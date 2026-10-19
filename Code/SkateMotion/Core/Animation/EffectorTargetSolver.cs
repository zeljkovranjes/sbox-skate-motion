namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds pinned and releasing target transforms from animated poses.</summary>
internal sealed class EffectorTargetSolver
{
	private readonly LimbSolverMath _angles;
	private readonly LimbArcSine _arcSine;
	private readonly EffectorTargetSettings _settings;

	internal EffectorTargetSolver( LimbSolverMath angles, LimbArcSine arcSine, EffectorTargetSettings settings )
	{
		ArgumentNullException.ThrowIfNull( angles );
		ArgumentNullException.ThrowIfNull( arcSine );
		if ( !Nonnegative( settings.PositionEpsilon ) || !Nonnegative( settings.MaximumDisplacement )
			|| !Nonnegative( settings.MaximumReleaseStep ) || !Nonnegative( settings.NormalThreshold )
			|| !Nonnegative( settings.OrientationDecay ) || !float.IsFinite( settings.MinimumAngle )
			|| !float.IsFinite( settings.MaximumAngle ) || settings.MinimumAngle > settings.MaximumAngle )
			throw new ArgumentException( "Target limits must be finite, nonnegative and ordered." );
		_angles = angles;
		_arcSine = arcSine;
		_settings = settings;
	}

	internal EffectorTargetResult Solve( EffectorTargetState state, LimbRigidTransform animated,
		LimbRigidTransform? reference, LimbRigidTransform modelToWorld, LimbRigidTransform worldToModel )
	{
		var world = modelToWorld.Compose( animated );
		var turn = LimbRigidTransform.Identity;
		if ( state.OrientationWeight > 0f )
		{
			turn = Orientation( state.Normal, state.OrientationWeight );
			if ( !state.OrientationRequested )
			{
				var weight = state.OrientationWeight - _settings.OrientationDecay;
				state = state with { OrientationWeight = -weight >= 0f ? 0f : weight };
			}
		}
		state = state.Mode == 3
			? Release( state, animated.Translation, modelToWorld )
			: state with { Release = worldToModel.Transform( state.Pin ) };
		var delta = Cap( state.Pin - world.Translation, _settings.MaximumDisplacement );

		// Keep the pivot compositions: cancelling translations algebraically changes rounding.
		var pivot = turn.Compose( LimbRigidTransform.Identity with { Translation = -world.Translation } );
		var moved = (LimbRigidTransform.Identity with { Translation = world.Translation + delta }).Compose( pivot );
		var target = moved.Compose( world );
		LimbRigidTransform? referenced = reference.HasValue
			? target.Compose( animated.RigidInverse().Compose( reference.Value ) ) : null;
		return new EffectorTargetResult( state, target, referenced );
	}

	private EffectorTargetState Release( EffectorTargetState state, Vector3 animatedPosition, LimbRigidTransform modelToWorld )
	{
		var desired = AddScaled( state.Release - animatedPosition, state.Weight, Scale( state.Offset, 1f - state.Weight ) );
		var step = Cap( desired - state.Offset, _settings.MaximumReleaseStep );
		var released = animatedPosition + desired;
		return state with { Release = released, Offset = state.Offset + step, Pin = modelToWorld.Transform( released ) };
	}

	private Vector3 Cap( Vector3 delta, float limit )
	{
		var length = SquareRoot( Dot( delta, delta ) );
		if ( !(length >= _settings.PositionEpsilon) )
			return delta;
		var amount = limit - length >= 0f ? length : limit;
		return Scale( Scale( delta, amount ), Reciprocal( length ) );
	}

	private LimbRigidTransform Orientation( Vector3 normal, float weight )
	{
		var axis = Cross( Vector3.UnitY, normal );
		var length = SquareRoot( Dot( axis, axis ) );
		if ( !(length > _settings.NormalThreshold) )
			return LimbRigidTransform.Identity;
		axis = Scale( axis, Reciprocal( length ) );
		var angle = _arcSine.Evaluate( length );
		angle = _settings.MinimumAngle - angle >= 0f ? _settings.MinimumAngle : angle;
		angle = _settings.MaximumAngle - angle >= 0f ? angle : _settings.MaximumAngle;
		var frame = _angles.AxisRotation( axis, weight * angle );
		return new LimbRigidTransform( frame, Vector3.Zero );
	}

	private static bool Nonnegative( float value ) => float.IsFinite( value ) && value >= 0f;
}
