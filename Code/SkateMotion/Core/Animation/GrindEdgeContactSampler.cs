using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Predicts an offset-plane crossing and projects it onto a bounded edge.</summary>
internal sealed class GrindEdgeContactSampler
{
	private readonly LimbSignedAngle _angle;
	private readonly GrindEdgeSampleSettings _settings;

	internal GrindEdgeContactSampler( LimbSignedAngle angle, GrindEdgeSampleSettings settings )
	{
		ArgumentNullException.ThrowIfNull( angle );
		_angle = angle;
		_settings = settings;
	}

	/// <summary>Failed samples preserve output. Successful samples retain its producer-owned edge index.</summary>
	internal bool Sample( GrindEdgeTrajectory trajectory, RailSegment edge, ref GrindEdgeSample output )
	{
		var difference = edge.End - edge.Start;
		var normal = Normalize( Cross( difference, Cross( Vector3.UnitY, difference ) ) );
		var length = SquareRoot( Dot( difference, difference ) );
		if ( length > _settings.MaximumEdgeVectorLength )
			difference = Scale( difference, MultiplyAdd( Reciprocal( length ), _settings.MaximumEdgeVectorLength, 0f ) );
		var clearance = AddScaled( normal, _settings.ContactOffset, Scale( normal, trajectory.Radius ) );
		if ( !CrossingTime( trajectory, edge.Start + clearance, normal, out var time ) )
			return false;
		var predicted = AddScaled( Scale( trajectory.Acceleration, .5f ), MultiplyAdd( time, time, 0f ),
			AddScaled( trajectory.Velocity, time, trajectory.Position ) );
		var center = predicted - clearance;
		var direction = NormalizeDirection( difference );
		var relative = center - edge.Start;
		var point = AddScaled( direction, Dot( direction, relative ), edge.Start );
		var expansion = Scale( difference, _settings.EndpointExtension );
		var overlap = Dot( point - (edge.Start - expansion), point - (edge.End + expansion) );
		if ( !(overlap < 0f) )
			return false;
		var cross = Cross( relative, direction );
		var tangent = NormalizeDirection( trajectory.Velocity - Scale( normal, Dot( normal, trajectory.Velocity ) ) );
		var planar = point - trajectory.Position;
		planar.Y = 0f;
		var angle = MathF.Abs( _angle.WrappedFraction( planar, direction, normal ) );
		if ( angle > _settings.HalfTurn * .5f )
			angle -= _settings.HalfTurn;
		output = new GrindEdgeSample( point, center, direction, tangent,
			SquareRoot( Dot( cross, cross ) ), time, MathF.Abs( angle ),
			(int)MathF.Truncate( time * _settings.SamplesPerSecond ), output.EdgeIndex, true );
		return true;
	}

	private Vector3 NormalizeDirection( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumDirectionLength ? Scale( value, inverse ) : Vector3.Zero;
	}

	private static bool CrossingTime( GrindEdgeTrajectory trajectory, Vector3 origin, Vector3 normal, out float time )
	{
		var a = Dot( normal, Scale( trajectory.Acceleration, Reciprocal( 2f ) ) );
		var b = Dot( normal, trajectory.Velocity );
		var c = Dot( normal, trajectory.Position ) - Dot( origin, normal );
		var discriminant = MultiplyAdd( b, b, 0f ) - MultiplyAdd( MultiplyAdd( a, 4f, 0f ), c, 0f );
		time = 0f;
		if ( discriminant < 0f )
			return false;
		// The quadratic helper has no linear fallback when acceleration along the normal is zero.
		var inverse = Reciprocal( MultiplyAdd( a, 2f, 0f ) );
		if ( !(discriminant > 0f) )
		{
			time = MultiplyAdd( inverse, -b, 0f );
			return time > 0f;
		}
		var root = SquareRoot( discriminant );
		var first = MultiplyAdd( inverse, -b + root, 0f );
		var second = MultiplyAdd( inverse, -b - root, 0f );
		// The two-root path selects the later root without requiring a positive time.
		time = MathF.Max( first, second );
		return true;
	}
}
