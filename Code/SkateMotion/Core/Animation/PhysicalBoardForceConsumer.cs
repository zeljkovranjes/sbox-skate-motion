namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Applies retained board forces to the primary body's physical accumulators.</summary>
internal static class PhysicalBoardForceConsumer
{
	internal static void Apply( PhysicalBoardForceQueue queue, PhysicalBodyState body,
		IPhysicalBoardForceProperties properties )
	{
		ArgumentNullException.ThrowIfNull( queue );
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( properties );
		if ( queue.Count == 0 )
			return;

		var height = properties.ReadApplicationHeight();
		foreach ( var entry in queue.Entries )
		{
			var point = entry.Application + new Vector3( 0f, height, 0f );
			var lever = body.Pose.Rotation.Transform( point, Vector3.Zero );
			ApplyAtLever( body, entry.Force, lever );
		}
		properties.ReadPostForceProperty();
	}

	internal static void ApplyAtLever( PhysicalBodyState body, Vector3 force, Vector3 lever )
	{
		body.LinearAccumulator += Scale( force, body.InverseMass );
		body.AngularAccumulator += InverseInertia( body ).Transform( Cross( lever, force ), Vector3.Zero );
		body.AccumulatorState = 0;
	}

	internal static LimbFrame InverseInertia( PhysicalBodyState body )
	{
		var d = body.InverseInertiaDiagonal;
		var o = body.InverseInertiaOffDiagonal;
		return new( new Vector3( d.X, o.X, o.Y ), new Vector3( o.X, d.Y, o.Z ), new Vector3( o.Y, o.Z, d.Z ) );
	}
}
