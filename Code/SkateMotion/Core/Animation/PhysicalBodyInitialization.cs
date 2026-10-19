namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Initializes a physical body while preserving its supplied default acceleration.</summary>
internal static class PhysicalBodyInitialization
{
	internal static void Initialize( PhysicalBodyState body, LimbRigidTransform frame,
		PhysicalBodyMass mass, int mode, int accumulatorState )
	{
		ArgumentNullException.ThrowIfNull( body );
		body.MotionMode = mode is 1 or 2 ? mode : 4;
		body.Mass = mode == 1 ? null : mass;
		body.InverseInertiaDiagonal = Vector3.Zero;
		body.InverseInertiaOffDiagonal = Vector3.Zero;
		body.InverseMass = 0f;
		PhysicalBodyBinding.WriteBody( body, frame );
		body.Velocity = Vector3.Zero;
		body.AngularVelocity = Vector3.Zero;
		body.LinearAccumulator = mode == 1 ? Vector3.Zero : body.DefaultLinearAccumulator;
		body.AngularAccumulator = Vector3.Zero;
		body.AccumulatorState = mode is 1 or 2 ? accumulatorState : 0;
		body.MotionMetric = mode == 1 ? 0f : float.MaxValue;
	}
}
