using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionCylinderAxes
{
	internal static int AppendSegmentCandidate( ReadOnlySpan<uint> inputs, uint offset,
		uint halfLength, Span<uint> axes, int count )
	{
		if ( inputs.Length < 16 || count < 0 || count >= axes.Length / 4 )
			throw new ArgumentException( "A segment candidate requires four input rows and a free output row." );
		var firstPoint = CollisionWordVector.Read( inputs, 0 );
		var firstAxis = CollisionWordVector.Read( inputs, 4 );
		var secondPoint = CollisionWordVector.Read( inputs, 8 );
		var secondAxis = CollisionWordVector.Read( inputs, 12 );
		var cross = firstAxis.Cross( secondAxis );
		var squared = cross.LengthSquared();
		CollisionWordVector candidate;
		if ( Greater( 0x007fffffu, squared ) )
		{
			var perpendicular = firstAxis.Cross( firstPoint.Subtract( secondPoint ) ).Cross( firstAxis );
			var perpendicularSquared = perpendicular.LengthSquared();
			candidate = Greater( 0x007fffffu, perpendicularSquared )
				? firstAxis : perpendicular.Scale( InverseRoot( perpendicularSquared ) );
		}
		else
		{
			var planeNormal = firstAxis.Cross( cross );
			var denominator = Dot( planeNormal, secondAxis );
			var numerator = Dot( planeNormal, firstPoint.Subtract( secondPoint ) );
			var reciprocal = Interpolate( denominator, ReciprocalEstimate( denominator ) );
			reciprocal = MultiplyAdd( reciprocal,
				NegativeMultiplySubtract( reciprocal, denominator, 0x3f800000u ), reciprocal );
			var parameter = MultiplyAdd( reciprocal, numerator, offset ^ 0x80000000u );
			if ( Greater( parameter, halfLength ) )
				candidate = EndpointCandidate( firstAxis, secondPoint, secondAxis, Add( halfLength, offset ) );
			else if ( Greater( halfLength ^ 0x80000000u, parameter ) )
				candidate = EndpointCandidate( firstAxis, secondPoint, secondAxis, Subtract( offset, halfLength ) );
			else
				candidate = cross.Scale( InverseRoot( squared ) );
		}
		candidate.Write( axes, count * 4 );
		return count + 1;
	}

	private static CollisionWordVector EndpointCandidate( CollisionWordVector firstAxis,
		CollisionWordVector secondPoint, CollisionWordVector secondAxis, uint parameter )
	{
		// The bounded endpoint is projected directly; the first point is not subtracted here.
		var endpoint = secondAxis.MultiplyAdd( parameter, secondPoint );
		var candidate = firstAxis.Cross( endpoint.Cross( firstAxis ) );
		return candidate.Scale( InverseRoot( candidate.LengthSquared() ) );
	}

	private static uint Dot( CollisionWordVector first, CollisionWordVector second ) =>
		Add( Add( Multiply( first.X, second.X ), Multiply( first.Y, second.Y ) ), Multiply( first.Z, second.Z ) );

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}
}
