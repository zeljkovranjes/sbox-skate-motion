namespace SkateMotion.Core.Animation;

/// <summary>Submits board queries before consuming its retained forces.</summary>
internal static class PhysicalBoardPostMotorController
{
	internal static void Update( bool suppressQueries, PhysicalBoardProbeState state,
		PhysicalBoardProbeInput input, ReadOnlySpan<PhysicalBodyBinding> bodies, int excludedActor,
		IPhysicalBoardProbeSink sink, PhysicalBoardForceQueue queue, PhysicalBodyState primaryBody,
		IPhysicalBoardForceProperties properties )
	{
		if ( !suppressQueries )
			PhysicalBoardProbeProducer.Update( state, input, bodies, excludedActor, sink );
		PhysicalBoardForceConsumer.Apply( queue, primaryBody, properties );
	}
}
