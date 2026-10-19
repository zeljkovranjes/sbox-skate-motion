namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Places an airborne rider around a predicted position while retaining local orientation.</summary>
internal sealed class PhysicalAirFrameController
{
	private readonly LimbSolverMath _math;

	internal PhysicalAirFrameController( LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
	}

	internal void Update( PhysicalRiderFrameState state, PhysicalAirFrameInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( state.ResetHeading )
		{
			state.LocalFrame = input.OrientationSource.RigidInverse().Compose( state.ModelToWorld )
				with { Translation = Vector3.Zero };
			state.ResetHeading = false;
		}

		if ( input.AdvanceRotation )
		{
			var angle = (input.PositiveRotation ? _math.Pi : -_math.Pi) / (float)input.RotationSteps;
			var (sine, cosine) = _math.SinCos( angle );
			var turn = new LimbRigidTransform( new LimbFrame(
				new Vector3( cosine, 0f, -sine ), Vector3.UnitY,
				new Vector3( sine, 0f, cosine ) ), Vector3.Zero );
			var local = state.LocalFrame.Compose( turn );
			state.LocalFrame = local with { Rotation = local.Rotation.Orthonormalized() };
		}

		var source = input.OrientationSource with { Translation = Vector3.Zero };
		var world = source.Compose( state.LocalFrame with { Translation = Vector3.Zero } );
		// Translation uses the composed axes before they are normalized.
		var translation = input.PredictedPosition - world.Rotation.Transform( input.ModelOffset, Vector3.Zero );
		state.ModelToWorld = new LimbRigidTransform( world.Rotation.Orthonormalized(), translation );
		state.WorldToModel = state.ModelToWorld.RigidInverse();
	}
}
