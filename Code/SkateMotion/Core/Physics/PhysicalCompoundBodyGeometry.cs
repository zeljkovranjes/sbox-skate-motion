using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Transformed cylinder and identity-frame sphere, with the retained pose volume and mass basis.</summary>
internal readonly record struct PhysicalCompoundBodyGeometry(
	LimbRigidTransform CylinderFrame, float CylinderRadius, float CylinderHalfLength,
	float SphereRadius, float PoseVolume, float MassBasis )
{
	internal static PhysicalCompoundBodyGeometry Create( PhysicalCompoundBodyDefinition definition, LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		var (sx, cx) = math.SinCos( definition.Angles.X );
		var (sy, cy) = math.SinCos( definition.Angles.Y );
		var (sz, cz) = math.SinCos( definition.Angles.Z );
		var sxcz = sx * cz;
		var cxcz = cx * cz;
		var cxsz = cx * sz;
		var sxsz = sx * sz;
		var rotation = new LimbFrame(
			new Vector3( cy * cz, cy * sz, -sy ),
			new Vector3( sy * sxcz - cxsz, MultiplyAdd( sy, sxsz, cxcz ), cy * sx ),
			new Vector3( MultiplyAdd( sy, cxcz, sxsz ), sy * cxsz - sxcz, cy * cx ) );
		var dimensions = definition.Dimensions;
		var volume = (dimensions.X * dimensions.Y) * dimensions.Z;
		var sphereRadius = MathF.Max( dimensions.X * .5f, dimensions.Y * .5f ) * definition.SizeScale;
		return new( new( rotation, definition.Position ), definition.CylinderRadius, definition.CylinderHeight * .5f,
			sphereRadius, volume, volume * definition.MassScale );
	}
}
