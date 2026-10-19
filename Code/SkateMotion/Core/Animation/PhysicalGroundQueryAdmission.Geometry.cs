namespace SkateMotion.Core.Animation;

using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;
using Writer = PhysicalGroundQueryCandidateWriter;

internal static partial class PhysicalGroundQueryAdmission
{
	private static void ProjectForward( Span<uint> owner, int index, float slope, uint flags, uint kind )
	{
		var origin = Project( owner, index < 0 ? Read( owner, 0 ) : Read( owner, Segment( index ) + 16 ) );
		var target = new Vector2( 2.8f, M.MultiplyAdd( slope, 2.8f - origin.X, origin.Y ) );
		var limit = (int)owner[ 0x6290 / 4 ];
		for ( var next = index + 1; next < limit; next++ )
		{
			var segment = Segment( next );
			var start = Read( owner, segment + 16 );
			var end = Read( owner, segment + 32 );
			var fraction = Intersect( Project( owner, start ), Project( owner, end ), origin, target );
			if ( fraction < 0f || fraction > 1f )
				continue;
			if ( owner[ segment / 4 ] == 0 )
			{
				var point = AddScaled( Read( owner, segment + 48 ), fraction * Float( owner, segment + 80 ), start );
				Writer.AppendPoint( owner, point, Read( owner, segment + 64 ), index, flags, kind );
			}
			else if ( Projection( owner, end ) <= Float( owner, 0x11c ) - 0.35f )
				Writer.AppendPoint( owner, end, FollowingNormal( owner, next, limit ), index, flags, kind );
			return;
		}
	}

	private static void ProjectBackward( Span<uint> owner, int index, float slope, uint flags, uint kind )
	{
		var origin = Project( owner, Read( owner, Segment( index ) + 32 ) );
		var target = new Vector2( -1f, M.MultiplyAdd( slope, -1f - origin.X, origin.Y ) );
		var limit = (int)owner[ 0x6290 / 4 ];
		for ( var previous = index - 1; previous >= 0; previous-- )
		{
			var segment = Segment( previous );
			var start = Read( owner, segment + 16 );
			var fraction = Intersect( Project( owner, start ), Project( owner, Read( owner, segment + 32 ) ), origin, target );
			if ( fraction < 0f )
			{
				if ( previous == 0 )
					Writer.AppendPoint( owner, Read( owner, 0 ), Read( owner, 32 ), index, flags, kind );
				continue;
			}
			if ( fraction > 1f )
				continue;
			var point = start;
			var normal = FollowingNormal( owner, previous, limit );
			if ( owner[ segment / 4 ] == 0 )
			{
				point = AddScaled( Read( owner, segment + 48 ), fraction * Float( owner, segment + 80 ), start );
				normal = Read( owner, segment + 64 );
			}
			if ( Projection( owner, point ) <= 0.75f )
				Writer.AppendPoint( owner, point, normal, index, flags, kind );
			return;
		}
	}

	private static void AdmitTravel( Span<uint> owner, float remaining )
	{
		var limit = (int)owner[ 0x6290 / 4 ];
		for ( var index = 0; index < limit; index++ )
		{
			var segment = Segment( index );
			var length = Float( owner, segment + 80 );
			var category = owner[ segment / 4 ];
			if ( category == 2 )
			{
				remaining += length / SlopeLimit( Read( owner, 64 ), length, false );
				continue;
			}
			remaining -= length;
			if ( remaining > 0f )
				continue;
			var end = Read( owner, segment + 32 );
			var point = category == 1 ? end : AddScaled( Read( owner, segment + 48 ), remaining, end );
			var normal = category == 1 ? FollowingNormal( owner, index, limit ) : Read( owner, segment + 64 );
			Writer.AppendPoint( owner, point, normal, index, 0, 9 );
			return;
		}
	}

	private static Vector4 FollowingNormal( ReadOnlySpan<uint> owner, int index, int limit ) =>
		index == limit - 1 ? Read( owner, 32 ) : Read( owner, Segment( index + 1 ) + 64 );

	private static Vector2 Project( ReadOnlySpan<uint> owner, Vector4 point ) =>
		new( Projection( owner, point ), Dot( point - Read( owner, 0 ), Read( owner, 32 ) ) );

	private static float Intersect( Vector2 start, Vector2 end, Vector2 origin, Vector2 target )
	{
		var line = target - origin;
		var segment = end - start;
		var offset = start - origin;
		var denominator = M.MultiplyAdd( line.Y, segment.X, 0f ) - M.MultiplyAdd( line.X, segment.Y, 0f );
		if ( MathF.Abs( denominator ) < 0.00001f )
			return 10000000000f;
		var numerator = M.MultiplyAdd( line.X, offset.Y, 0f ) - M.MultiplyAdd( line.Y, offset.X, 0f );
		return M.MultiplyAdd( 1f / denominator, numerator, 0f );
	}

	private static float Slope( ReadOnlySpan<uint> owner, Vector4 start, Vector4 end )
	{
		var delta = end - start;
		var horizontal = Dot( Read( owner, 16 ), delta );
		return horizontal < 0.001f ? 1000f : M.MultiplyAdd( Dot( Read( owner, 32 ), delta ), M.Reciprocal( horizontal ), 0f );
	}

	private static float SlopeLimit( Vector4 velocity, float length, bool rising )
	{
		var speed = Math.Clamp( Length( velocity ), 2f, 6f );
		var basis = length <= 0.4f ? 30f : 50f;
		var gradient = length <= 0.4f || rising ? -3.75f : -5f;
		return Tangent( M.MultiplyAdd( speed - 2f, gradient, basis ) * Bits( 0x3c8efa35 ) );
	}

	private static float Tangent( float angle )
	{
		var scaled = M.MultiplyAdd( angle, Bits( 0x3f22f983 ), 0f );
		var quadrant = (int)(scaled + MathF.CopySign( 0.5f, scaled ));
		var reduced = M.SubtractProduct( quadrant, Bits( 0x3fc90fda ), angle );
		reduced = M.SubtractProduct( quadrant, Bits( 0x33a22169 ), reduced );
		var square = M.MultiplyAdd( reduced, reduced, 0f );
		var cube = M.MultiplyAdd( square, reduced, 0f );
		var denominator = M.MultiplyAdd( square, Bits( 0x3c1f166d ), Bits( 0xbedbb51e ) );
		denominator = M.MultiplyAdd( denominator, square, 1f );
		var numerator = M.MultiplyAdd( cube, Bits( 0xbdc42983 ), reduced );
		// Each quotient has one residual correction after the rounded reciprocal seed.
		var reciprocal = 1f / ((quadrant & 1) == 0 ? denominator : numerator);
		var divisor = (quadrant & 1) == 0 ? denominator : numerator;
		var dividend = (quadrant & 1) == 0 ? numerator : denominator;
		var quotient = M.MultiplyAdd( dividend, reciprocal, -0f );
		var error = M.SubtractProduct( divisor, reciprocal, 1f );
		var corrected = M.MultiplyAdd( error, quotient, quotient );
		var result = float.IsNaN( corrected ) ? quotient : corrected;
		return (quadrant & 1) == 0 ? result : -result;
	}

	private static int Count( ReadOnlySpan<uint> owner ) => (int)owner[ 0x76f0 / 4 ];
	private static int Segment( int index ) => 0x4a90 + index * 96;
	private static float Bits( uint bits ) => BitConverter.UInt32BitsToSingle( bits );
	private static float Float( ReadOnlySpan<uint> words, int offset ) => Bits( words[ offset / 4 ] );
	private static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		Float( words, offset ), Float( words, offset + 4 ), Float( words, offset + 8 ), Float( words, offset + 12 ) );
	private static float Dot( Vector4 left, Vector4 right ) =>
		(M.MultiplyAdd( left.X, right.X, 0f ) + M.MultiplyAdd( left.Y, right.Y, 0f )) + M.MultiplyAdd( left.Z, right.Z, 0f );
	private static float Projection( ReadOnlySpan<uint> owner, Vector4 point ) => Dot( Read( owner, 16 ), point - Read( owner, 0 ) );
	private static float Length( Vector4 value ) => M.SquareRoot( Dot( value, value ) );
	private static Vector4 Scale( Vector4 value, float scale ) => AddScaled( value, scale, Vector4.Zero );
	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 add ) => new(
		M.MultiplyAdd( value.X, scale, add.X ), M.MultiplyAdd( value.Y, scale, add.Y ),
		M.MultiplyAdd( value.Z, scale, add.Z ), M.MultiplyAdd( value.W, scale, add.W ) );
	private static Vector4 Cross( Vector4 left, Vector4 right ) => new(
		M.SubtractProduct( right.Y, left.Z, M.MultiplyAdd( left.Y, right.Z, 0f ) ),
		M.SubtractProduct( right.Z, left.X, M.MultiplyAdd( left.Z, right.X, 0f ) ),
		M.SubtractProduct( right.X, left.Y, M.MultiplyAdd( left.X, right.Y, 0f ) ),
		M.SubtractProduct( right.W, left.W, M.MultiplyAdd( left.W, right.W, 0f ) ) );
}
