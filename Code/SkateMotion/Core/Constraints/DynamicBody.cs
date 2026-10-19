namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal struct DynamicBody
{
	private Quaternion _rotation;

	internal Vector3 Position { get; set; }
	internal Quaternion Rotation
	{
		get => _rotation;
		set
		{
			_rotation = value;
			RotationBasis = null;
		}
	}
	internal Matrix4x4? RotationBasis { get; set; }
	internal Vector3 Velocity { get; set; }
	internal Vector3 AngularVelocity { get; set; }
	internal Vector3 Acceleration { get; set; }
	internal Vector3 AngularAcceleration { get; set; }
	internal float InverseMass { get; set; }
	internal Matrix4x4 WorldInverseInertia { get; set; }
	internal float MotionMetric { get; set; }
	internal int QuietSteps { get; set; }
	internal int MotionFlags { get; set; }
}
