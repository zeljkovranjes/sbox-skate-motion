namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;
using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Connects the non-launch active callback, board session, queries, forces and final publication.</summary>
internal sealed class PhysicalBoardOwnerStatePhase
{
	private readonly PhysicalNonJumpMotionController _motor;
	private readonly PhysicalBoardSession _session;
	private readonly IPhysicalBoardOwnerObservations _observations;
	private readonly IPhysicalBoardProbeSink _queries;
	private readonly IPhysicalBoardForceProperties _properties;

	internal PhysicalBoardOwnerStatePhase( PhysicalNonJumpMotionController motor, PhysicalBoardSession session,
		IPhysicalBoardOwnerObservations observations, IPhysicalBoardProbeSink queries, IPhysicalBoardForceProperties properties )
	{
		ArgumentNullException.ThrowIfNull( motor );
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( observations );
		ArgumentNullException.ThrowIfNull( queries );
		ArgumentNullException.ThrowIfNull( properties );
		_motor = motor;
		_session = session;
		_observations = observations;
		_queries = queries;
		_properties = properties;
	}

	internal void UpdateNonJump( PhysicalBoardOwnerPhaseState state, PhysicalJumpCharacterState motor,
		PhysicalCharacterState character, PhysicalRiderFrameState placement, PhysicalBoardForceState forces,
		PhysicalDirectionState direction, PhysicalNonJumpMotionInput input, PhysicalBodyState primaryBody,
		PhysicalBoardForceQueue queue, PhysicalBoardProbeState probes )
	{
		ArgumentNullException.ThrowIfNull( state );
		_motor.Update( motor, character, placement, forces, direction, input, primaryBody, queue );
		if ( _observations.BoardSessionEnabled )
			_session.Update( true, _observations.ReadBoardInput(), _observations.ReadBoardPoses() );
		var suppress = _observations.SuppressQueries;
		if ( !suppress )
			PhysicalBoardProbeProducer.Update( probes, _observations.ReadProbeInput(),
				character.BoardBodies, _observations.ExcludedActor, _queries );
		PhysicalBoardForceConsumer.Apply( queue, primaryBody, _properties );
		state.PredictedDisplacement = PredictDisplacement( primaryBody );
		state.Elapsed += _observations.DeltaTime;
	}

	internal static Vector3 PredictDisplacement( PhysicalBodyState body )
	{
		ArgumentNullException.ThrowIfNull( body );
		var angular = body.AngularAccumulator;
		var linear = body.LinearAccumulator;
		// Preserve the source's zero-offset angular operations and the two separate timestep scales.
		var acceleration = new Vector3(
			MultiplyAdd( 0f, angular.Y, SubtractProduct( 0f, angular.Z, linear.X ) ),
			MultiplyAdd( 0f, angular.Z, SubtractProduct( 0f, angular.X, linear.Y ) ),
			MultiplyAdd( 0f, angular.X, SubtractProduct( 0f, angular.Y, linear.Z ) ) );
		return Scale( Scale( acceleration, 1f / 60f ), 1f / 60f ) + Scale( body.Velocity, 1f / 60f );
	}
}
