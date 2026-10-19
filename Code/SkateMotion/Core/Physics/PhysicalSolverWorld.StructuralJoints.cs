using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;

internal sealed partial class PhysicalSolverWorld
{
	internal JointConstraint CompileStructuralJoint( in PhysicalStructuralJoint joint,
		int childBody, int parentBody, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		if ( (uint)childBody >= (uint)BodyCount || (uint)parentBody >= (uint)BodyCount )
			throw new ArgumentOutOfRangeException( nameof( childBody ), "Resolve both joint endpoints to registered world bodies." );
		var frame = joint.Frame;
		var limits = joint.Limits;
		var linear = new JointGeometry( childBody, parentBody, frame.ChildPosition, frame.BodyPosition,
			StructuralRotation( joint.LinearRotation ), Vector3.Zero,
			new Vector3( limits.LinearSpeed.X, limits.LinearSpeed.Y, limits.LinearSpeed.Z ) );
		// The retained speed fields preserve their serialized order. The first channel
		// controls the axial direction, and the second controls both transverse axes.
		var angular = new RangeJointGeometry( StructuralRotation( frame.ChildRotation ),
			StructuralRotation( frame.BodyRotation ), limits.SwingCosine, limits.TwistCosine,
			limits.SwingSpeed, limits.TwistSpeed, limits.SwingMode, limits.TwistMode );
		return RangeJointCompiler.Compile( _bodies[ childBody ], _bodies[ parentBody ], linear, angular, step );
	}

	private static Quaternion StructuralRotation( Vector4 value ) => new( value.X, value.Y, value.Z, value.W );
}
