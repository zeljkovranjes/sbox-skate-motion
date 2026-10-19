namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySamples
{
	internal static void Refine( Span<uint> owner )
	{
		SortMain( owner );
		PhysicalGroundQuerySampleSort.Sort( owner, 0x3a60, (int)owner[ 0x4a60 / 4 ] );
		PromoteObstacles( owner );
		RefineNormals( owner );
		RemoveRedundant( owner );
	}

	private static void PromoteObstacles( Span<uint> owner )
	{
		var count = (int)owner[ 0x3a50 / 4 ];
		for ( var index = 1; index < count; index++ )
		{
			var previous = 0x2a50 + (index - 1) * 64;
			var current = previous + 64;
			var a = Read( owner, previous + 32 );
			var b = Read( owner, current + 32 );
			var minimum = MathF.Min( a.Y, b.Y );
			var maximum = MathF.Max( a.Y, b.Y );
			for ( var candidate = 0; candidate < owner[ 0x4a60 / 4 ]; candidate++ )
			{
				var offset = 0x3a60 + candidate * 64;
				var point = Read( owner, offset + 32 );
				if ( a.X > point.X || point.X > b.X )
					continue;
				if ( minimum > point.Y )
				{
					owner[ (offset + 48) / 4 ] |= 2;
					continue;
				}
				if ( maximum > point.Y || Float( owner, previous + 52 ) > Float( owner, offset + 52 )
					|| Float( owner, current + 52 ) < Float( owner, offset + 52 ) )
					continue;
				owner[ (offset + 48) / 4 ] |= 2;
				var lowestAbove = offset;
				var highest = offset;
				candidate++;
				for ( ; candidate < owner[ 0x4a60 / 4 ]; candidate++ )
				{
					var next = 0x3a60 + candidate * 64;
					var projected = Read( owner, next + 32 );
					if ( MathF.Abs( point.X - projected.X ) > 0.001f )
						break;
					owner[ (next + 48) / 4 ] |= 2;
					if ( projected.Y >= maximum && Read( owner, lowestAbove + 32 ).Y > projected.Y )
						lowestAbove = next;
					if ( projected.Y > Read( owner, highest + 32 ).Y )
						highest = next;
				}
				owner[ (lowestAbove + 48) / 4 ] = (owner[ (lowestAbove + 48) / 4 ] & ~2u) | 1;
				owner[ (highest + 48) / 4 ] = (owner[ (highest + 48) / 4 ] & ~2u) | 1;
				var normal = Read( owner, lowestAbove + 16 );
				Append( owner, Read( owner, lowestAbove ), ref normal, 1, Float( owner, lowestAbove + 52 ), 1 );
				ExpandHeights( Read( owner, lowestAbove + 32 ).Y, ref minimum, ref maximum );
				if ( highest != lowestAbove )
				{
					ExpandHeights( Read( owner, highest + 32 ).Y, ref minimum, ref maximum );
					normal = Read( owner, highest + 16 );
					Append( owner, Read( owner, highest ), ref normal, 1, Float( owner, highest + 52 ), 1 );
				}
			}
		}
		SortMain( owner );
	}

	private static void ExpandHeights( float value, ref float minimum, ref float maximum )
	{
		if ( value > maximum )
		{
			minimum = maximum;
			maximum = value;
		}
		else if ( value > minimum )
			minimum = value;
	}

	private static void RefineNormals( Span<uint> owner )
	{
		var previousPoint = Read( owner, 0 );
		var up = Read( owner, 32 );
		var forward = Read( owner, 16 );
		for ( var index = 0; index < owner[ 0x3a50 / 4 ]; index++ )
		{
			var offset = 0x2a50 + index * 64;
			var point = Read( owner, offset );
			var height = Dot( point - previousPoint, up );
			var normal = Read( owner, offset + 16 );
			if ( MathF.Abs( height ) < 0.001f )
				normal = up;
			else
				ConstrainNormal( height > 0f ? -forward : forward, up, ref normal );
			Write( owner, offset + 16, normal );
			previousPoint = point;
		}
		var count = (int)owner[ 0x3a50 / 4 ];
		for ( var index = 1; index < count; index++ )
		{
			var previous = 0x2a50 + (index - 1) * 64;
			var current = previous + 64;
			for ( var candidate = 0; candidate < owner[ 0x4a60 / 4 ]; candidate++ )
			{
				var offset = 0x3a60 + candidate * 64;
				var x = Float( owner, offset + 32 );
				if ( (owner[ (offset + 48) / 4 ] & 3) == 0
					&& x >= Float( owner, previous + 32 ) && x <= Float( owner, current + 32 ) )
					IntersectSupport( owner, previous, current, offset );
			}
		}
		SortMain( owner );
	}

	private static void IntersectSupport( Span<uint> owner, int previous, int current, int obstacle )
	{
		var a = Read( owner, previous );
		var b = Read( owner, current );
		var firstNormal = Read( owner, previous + 16 );
		var secondNormal = Read( owner, current + 16 );
		var point = Read( owner, obstacle );
		var normal = Read( owner, obstacle + 16 );
		var delta = b - a;
		if ( Dot( firstNormal, secondNormal ) > 0.99f && Dot( firstNormal, normal ) > 0.99f
			&& MathF.Abs( Dot( delta, secondNormal ) ) < 0.01f )
		{
			var offset = point - a;
			if ( Length( offset ) < 0.01f )
				offset = point - b;
			if ( MathF.Abs( Dot( offset, secondNormal ) ) < 0.01f )
				return;
		}
		var firstProjection = Read( owner, previous + 32 );
		var secondProjection = Read( owner, current + 32 );
		ConstrainNormal( secondProjection.Y > firstProjection.Y ? -Read( owner, 16 ) : Read( owner, 16 ),
			Read( owner, 32 ), ref normal );
		var firstTrim = AddScaled( delta - Scale( firstNormal, Dot( firstNormal, delta ) ), 1.1f, a );
		var lastTrim = b - Scale( delta - Scale( secondNormal, Dot( secondNormal, delta ) ), 1.1f );
		var firstFound = IntersectPlane( point, normal, a, firstTrim, out var first );
		var lastFound = IntersectPlane( point, normal, lastTrim, b, out var last );
		var inverseLength = 1f / Length( delta );
		var keyRange = Float( owner, current + 52 ) - Float( owner, previous + 52 );
		for ( var index = 0; index < 2; index++ )
		{
			if ( index == 0 ? !firstFound : !lastFound )
				continue;
			var intersection = index == 0 ? first : last;
			var projected = Project( owner, intersection );
			if ( projected.X < firstProjection.X || projected.X > secondProjection.X
				|| projected.Y < MathF.Min( firstProjection.Y, secondProjection.Y )
				|| projected.Y > MathF.Max( firstProjection.Y, secondProjection.Y ) )
				continue;
			var key = M.MultiplyAdd( inverseLength, keyRange * Length( intersection - a ), Float( owner, previous + 52 ) );
			var emittedNormal = normal;
			Append( owner, intersection, ref emittedNormal, 1, key, 2 );
			owner[ (obstacle + 48) / 4 ] |= 4;
		}
	}

	private static void RemoveRedundant( Span<uint> owner )
	{
		Set( owner, 0x11c, 10000000000f );
		var count = (int)owner[ 0x3a50 / 4 ];
		if ( count <= 1 )
			return;
		var remaining = count;
		var removeKey = Float( owner, 0x2a50 + (count - 1) * 64 + 52 ) + 1f;
		for ( var index = 1; index < count - 1; index++ )
		{
			var current = 0x2a50 + index * 64;
			if ( (owner[ (current + 48) / 4 ] & 8) != 0 )
			{
				Set( owner, 0x11c, Dot( Read( owner, current ) - Read( owner, 0 ), Read( owner, 16 ) ) );
				remaining += index + 1 - count;
				break;
			}
			var point = Read( owner, current + 32 );
			var first = point - Read( owner, current - 32 );
			var second = Read( owner, current + 96 ) - point;
			first.Z = second.Z = 0f;
			first.W = second.W = 0f;
			var firstLength = Length( first );
			var secondLength = Length( second );
			var remove = firstLength < 0.02f;
			if ( !remove && secondLength >= 0.02f
				&& !(Dot( first, second ) < 0f && Dot( first, Read( owner, 32 ) ) >= 0f) )
				remove = Length( Cross( Scale( first, M.Reciprocal( firstLength ) ), Scale( second, M.Reciprocal( secondLength ) ) ) ) < 0.05f;
			if ( remove )
			{
				Set( owner, current + 52, removeKey );
				remaining--;
			}
		}
		SortMain( owner );
		owner[ 0x3a50 / 4 ] = (uint)remaining;
	}

	private static void SortMain( Span<uint> owner ) =>
		PhysicalGroundQuerySampleSort.Sort( owner, 0x2a50, (int)owner[ 0x3a50 / 4 ] );
}

