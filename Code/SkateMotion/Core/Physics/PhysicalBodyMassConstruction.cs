using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Constructs inverse body mass and a local correction from supplied shape properties.</summary>
internal static class PhysicalBodyMassConstruction
{
	internal static PhysicalBodyMassResult Create( PhysicalShapeMassProperties? properties,
		LimbRigidTransform massFrame, float requestedMass, float density, float axisTolerance )
	{
		var shape = properties ?? new PhysicalShapeMassProperties( new Vector3( .1666666716337204f ), 1f );
		if ( !properties.HasValue )
			massFrame = LimbRigidTransform.Identity;
		var mass = requestedMass < 1.1754943508222875e-38f ? shape.Volume * density : requestedMass;
		var inertia = shape.UnitInertia * mass;
		var inverse = new Vector3( Reciprocal( inertia.X ), Reciprocal( inertia.Y ), Reciprocal( inertia.Z ) );
		var minimumInverse = MathF.Min( MathF.Min( inverse.X, inverse.Y ), inverse.Z );
		var extent = (shape.UnitInertia.X + shape.UnitInertia.Y) + shape.UnitInertia.Z;
		var position = massFrame.Translation;
		var squared = ((position.X * position.X) + (position.Y * position.Y)) + (position.Z * position.Z);
		var needsOffset = squared >= extent * 1.0000001111620804e-6f
			|| DifferentAxis( massFrame.Rotation.X, Vector3.UnitX, axisTolerance )
			|| DifferentAxis( massFrame.Rotation.Y, Vector3.UnitY, axisTolerance )
			|| DifferentAxis( massFrame.Rotation.Z, Vector3.UnitZ, axisTolerance );
		return new( new PhysicalBodyMass( inverse, 1f / mass ),
			needsOffset ? massFrame.RigidInverse() : null, 1f / minimumInverse, shape.Volume, properties.HasValue );
	}

	private static bool DifferentAxis( Vector3 axis, Vector3 reference, float tolerance )
	{
		var difference = Vector3.Abs( axis - reference );
		return difference.X > tolerance || difference.Y > tolerance || difference.Z > tolerance;
	}
}
