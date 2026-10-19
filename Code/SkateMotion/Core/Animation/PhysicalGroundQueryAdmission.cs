namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;
using Writer = PhysicalGroundQueryCandidateWriter;

internal static partial class PhysicalGroundQueryAdmission
{
	/// <summary>Rebuilds the admitted candidates from the retained query frame and classified segments.</summary>
	internal static int Build( Span<uint> owner )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount
			|| owner[ 0x6290 / 4 ] > owner[ 0x6294 / 4 ] || owner[ 0x6294 / 4 ] > 64 )
			throw new ArgumentException( "A complete query owner with valid segment counts is required.", nameof( owner ) );
		owner[ 0x76f0 / 4 ] = 0;
		var position = Read( owner, 0 );
		var up = Read( owner, 32 );
		var velocity = Read( owner, 64 );
		var supported = (owner[ 0x120 / 4 ] & 1) != 0;
		if ( supported && Length( velocity - Scale( up, Dot( up, velocity ) ) ) < 0.01f )
		{
			var normal = Read( owner, 0x80 );
			Writer.Append( owner, Read( owner, 0x70 ), normal, Cross( Read( owner, 48 ), normal ), -1, 0, 0 );
			return Count( owner );
		}
		var limit = (int)owner[ 0x6290 / 4 ];
		if ( limit == 0 )
			return 0;
		var highest = -1;
		var risingSeen = false;
		var descendingSeen = false;
		for ( var index = 0; index < limit; index++ )
		{
			var segment = Segment( index );
			var category = owner[ segment / 4 ];
			var normal = Read( owner, segment + 64 );
			if ( index + 1 < limit )
				normal = category == 0 || owner[ Segment( index + 1 ) / 4 ] == 0
					? Read( owner, Segment( index + 1 ) + 64 ) : up;
			if ( !descendingSeen )
			{
				var previous = highest < 0 ? position : Read( owner, Segment( highest ) + 32 );
				if ( Dot( Read( owner, segment + 32 ) - previous, up ) > 0f )
					highest = index;
			}
			var length = Float( owner, segment + 80 );
			if ( length <= 0.05f )
				continue;
			if ( category == 1 )
				AdmitRise( owner, index, limit, length, normal, ref risingSeen );
			else if ( category == 2 )
				AdmitDescent( owner, index, limit, length, normal, highest, risingSeen, ref descendingSeen );
		}
		if ( supported )
		{
			var height = position.Y - Read( owner, 0x70 ).Y;
			if ( height > 0.01f )
			{
				var flags = owner[ 0x76d0 / 4 ] & 0x140;
				if ( height >= 0.4f )
					flags |= 0x40;
				if ( Count( owner ) == 0 || !descendingSeen )
					ProjectForward( owner, -1, -SlopeLimit( velocity, 0.8f, false ), flags,
						Count( owner ) == 0 ? 7u : 8u );
				return Count( owner );
			}
		}
		if ( Count( owner ) == 0 )
			AdmitTravel( owner, Length( velocity ) * Bits( 0x3c888889 ) );
		return Count( owner );
	}

	private static void AdmitRise( Span<uint> owner, int index, int limit, float length,
		Vector4 normal, ref bool risingSeen )
	{
		var segment = Segment( index );
		var slope = SlopeLimit( Read( owner, 64 ), length, true );
		var futureRise = false;
		var withinExtent = false;
		var merged = false;
		for ( var next = index + 1; next < limit; next++ )
		{
			var offset = Segment( next );
			var category = owner[ offset / 4 ];
			if ( !withinExtent && category == 2 )
			{
				futureRise = Projection( owner, Read( owner, offset + 16 ) ) < Float( owner, 0x11c ) - 0.35f;
				if ( futureRise )
					withinExtent = true;
			}
			if ( category != 0 )
			{
				if ( category != 1 )
					break;
				if ( Float( owner, offset + 80 ) >= 0.05f )
				{
					var nextEnd = Read( owner, offset + 32 );
					var connectingSlope = Slope( owner, Read( owner, segment + 32 ), nextEnd );
					if ( SlopeLimit( Read( owner, 64 ), length, true ) * 0.5f < connectingSlope )
					{
						merged = true;
						if ( !risingSeen )
							merged = Slope( owner, Read( owner, 0 ), nextEnd )
								>= SlopeLimit( Read( owner, 64 ), length, true ) * 0.5f;
						slope = connectingSlope;
					}
					break;
				}
			}
			if ( !withinExtent && Dot( Read( owner, offset + 48 ), Read( owner, 16 ) ) > 0.707f
				&& Float( owner, 0x11c ) - 0.35f > Projection( owner, Read( owner, offset + 16 ) ) )
			{
				futureRise = true;
				withinExtent = true;
			}
		}
		if ( !risingSeen )
			risingSeen = futureRise;
		if ( !withinExtent )
			return;
		var tall = length > 0.4f;
		ProjectBackward( owner, index, slope, tall ? 0x80u : 0u, 1 );
		Writer.AppendPoint( owner, Read( owner, segment + 32 ), normal, index,
			!merged && tall ? 0x140u : 0x40u, merged ? 2u : 3u );
	}

	private static void AdmitDescent( Span<uint> owner, int index, int limit, float length,
		Vector4 normal, int highest, bool risingSeen, ref bool descendingSeen )
	{
		var segment = Segment( index );
		var slope = -SlopeLimit( Read( owner, 64 ), length, false );
		var merged = false;
		for ( var next = index + 1; next < limit; next++ )
		{
			var offset = Segment( next );
			var category = owner[ offset / 4 ];
			if ( category == 0 )
				continue;
			if ( category != 2 )
				break;
			if ( Float( owner, offset + 80 ) < 0.05f )
				continue;
			var connectingSlope = Slope( owner, Read( owner, segment + 16 ), Read( owner, offset + 16 ) );
			if ( SlopeLimit( Read( owner, 64 ), length, false ) * -0.5f > connectingSlope )
			{
				slope = connectingSlope;
				merged = true;
			}
			break;
		}
		if ( !descendingSeen && !risingSeen && highest >= 0
			&& !(index == 1 && owner[ Segment( 0 ) / 4 ] == 0)
			&& Dot( Read( owner, Segment( highest ) + 32 ) - Read( owner, 0 ), Read( owner, 32 ) ) > 0.02f )
		{
			descendingSeen = true;
			return;
		}
		if ( merged )
			Writer.AppendPoint( owner, Read( owner, segment + 16 ), normal, index, 0x40, 5 );
		else
		{
			if ( Projection( owner, Read( owner, segment + 16 ) ) > 0.75f )
			{
				descendingSeen = true;
				return;
			}
			var tall = length > 0.4f;
			ProjectForward( owner, index, slope, tall ? 0x100u : 0u, 4 );
			Writer.AppendPoint( owner, Read( owner, segment + 16 ), normal, index, tall ? 0xc0u : 0x40u, 6 );
		}
		descendingSeen = true;
	}
}
