namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Blends pose columns using supplied angular profiles and branch thresholds.</summary>
internal sealed class LimbPoseBlender
{
	private readonly LimbSolverMath _angles;
	private readonly LimbArcTangent _arcTangent;
	private readonly float _halfTurnThreshold;
	private readonly float _linearAngleThreshold;

	internal LimbPoseBlender( LimbSolverMath angles, LimbArcTangent arcTangent,
		float halfTurnThreshold, float linearAngleThreshold )
	{
		ArgumentNullException.ThrowIfNull( angles );
		ArgumentNullException.ThrowIfNull( arcTangent );
		if ( !float.IsFinite( halfTurnThreshold ) || halfTurnThreshold < 0f
			|| !float.IsFinite( linearAngleThreshold ) || linearAngleThreshold < 0f )
			throw new ArgumentException( "Blend thresholds must be finite and nonnegative." );
		_angles = angles;
		_arcTangent = arcTangent;
		_halfTurnThreshold = halfTurnThreshold;
		_linearAngleThreshold = linearAngleThreshold;
	}

	internal LimbPoseBlendResult Blend( LimbRigidTransform left, LimbRigidTransform right, float weight )
	{
		if ( weight >= 1f )
			return new LimbPoseBlendResult( right, 0f );
		var a = left.Rotation;
		var b = right.Rotation;
		var relative = new LimbFrame(
			b.Transform( new Vector3( a.X.X, a.Y.X, a.Z.X ), Vector3.Zero ),
			b.Transform( new Vector3( a.X.Y, a.Y.Y, a.Z.Y ), Vector3.Zero ),
			b.Transform( new Vector3( a.X.Z, a.Y.Z, a.Z.Z ), Vector3.Zero ) );
		var skew = new Vector3( relative.Y.Z - relative.Z.Y,
			relative.Z.X - relative.X.Z, relative.X.Y - relative.Y.X );
		var trace = ((relative.X.X + relative.Y.Y) + relative.Z.Z) - 1f;
		var length = SquareRoot( Dot( skew, skew ) );
		var axis = length > 0f ? Scale( skew, Reciprocal( length ) ) : Vector3.Zero;
		var angle = _arcTangent.Evaluate( length, trace );
		if ( length <= _halfTurnThreshold && trace <= 0f )
			axis = HalfTurnAxis( relative );
		if ( weight <= 0f )
			return new LimbPoseBlendResult( left, angle );

		LimbFrame rotation;
		if ( angle < _linearAngleThreshold )
		{
			// Each column is normalized independently; no orthogonalization follows.
			rotation = new LimbFrame( Normalize( AddScaled( b.X - a.X, weight, a.X ) ),
				Normalize( AddScaled( b.Y - a.Y, weight, a.Y ) ), Normalize( AddScaled( b.Z - a.Z, weight, a.Z ) ) );
		}
		else
		{
			var turn = _angles.AxisRotation( axis, MultiplyAdd( angle, weight, 0f ) );
			rotation = new LimbFrame( turn.Transform( a.X, Vector3.Zero ),
				turn.Transform( a.Y, Vector3.Zero ), turn.Transform( a.Z, Vector3.Zero ) );
		}
		var translation = AddScaled( right.Translation - left.Translation, weight, left.Translation );
		return new LimbPoseBlendResult( new LimbRigidTransform( rotation, translation ),
			angle - MultiplyAdd( angle, weight, 0f ) );
	}

	internal LimbRigidTransform BlendTransform( LimbRigidTransform left, LimbRigidTransform right, float weight )
	{
		var result = Blend( left, right, weight ).Transform;
		// Translation uses separate products and remains extrapolated at clamped rotation endpoints.
		return result with { Translation = Scale( left.Translation, 1f - weight ) + Scale( right.Translation, weight ) };
	}

	private static Vector3 HalfTurnAxis( LimbFrame relative )
	{
		var x = relative.X;
		var y = relative.Y;
		var z = relative.Z;
		var index = x.X > y.Y ? (x.X > z.Z ? 0 : 2) : (y.Y > z.Z ? 1 : 2);
		var diagonal = 1f + (index == 0 ? x.X : index == 1 ? y.Y : z.Z);
		var squared = MultiplyAdd( diagonal, diagonal, 0f );
		var candidate = index switch
		{
			0 => new Vector3( squared, x.Y + y.X, x.Z + z.X ),
			1 => new Vector3( y.X + x.Y, squared, y.Z + z.Y ),
			_ => new Vector3( z.X + x.Z, z.Y + y.Z, squared )
		};
		return Normalize( candidate );
	}
}
