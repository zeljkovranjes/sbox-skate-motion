namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds rider placement from the physical deck and a supplied orientation frame.</summary>
internal sealed class PhysicalRiderFrameController
{
	private readonly LimbSolverMath _math;
	private readonly LimbSignedAngle _angles;

	internal PhysicalRiderFrameController( LimbSolverMath math, LimbSignedAngle angles )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( angles );
		_math = math;
		_angles = angles;
	}

	internal void Update( PhysicalRiderFrameState state, PhysicalBodyBinding deck,
		LimbRigidTransform orientationSource, float deltaTime )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( deck );
		state.PreviousBoardPosition = state.BoardFrame.Translation;
		state.BoardFrame = deck.Read();
		state.WorldToBoard = state.BoardFrame.RigidInverse();
		if ( !state.PreserveAnchor )
		{
			if ( deck.Body is null )
				throw new InvalidOperationException( "Predicting the rider anchor requires a physical deck body." );
			state.Anchor = AddScaled( deck.Body.Velocity, deltaTime, state.BoardFrame.Translation );
		}
		state.PreserveAnchor = false;

		if ( state.ResetHeading )
		{
			var forward = state.RootFrame.Rotation.Z with { Y = 0f };
			var angle = _angles.Evaluate( forward, Vector3.UnitZ, Vector3.UnitY );
			var (sine, cosine) = _math.SinCos( angle );
			state.LocalFrame = new LimbRigidTransform( new LimbFrame(
				new Vector3( cosine, 0f, -sine ), Vector3.UnitY,
				new Vector3( sine, 0f, cosine ) ), Vector3.Zero );
			state.ResetHeading = false;
		}

		var rootOffset = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, -state.RootFrame.Translation );
		state.RelativeFrame = orientationSource.Compose( state.LocalFrame.Compose( rootOffset ) );
		var anchorFrame = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, state.Anchor );
		var world = anchorFrame.Compose( state.RelativeFrame );
		state.ModelToWorld = world with { Rotation = world.Rotation.Orthonormalized() };
		state.WorldToModel = state.ModelToWorld.RigidInverse();
	}
}
