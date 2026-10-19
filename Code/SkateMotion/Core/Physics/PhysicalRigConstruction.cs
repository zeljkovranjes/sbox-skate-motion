using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Assembles the physical and animation identities needed by a shared rig owner.</summary>
internal sealed class PhysicalRigConstruction
{
	internal EffectorSkeletonHierarchy Hierarchy { get; }
	internal EffectorRigState Effectors { get; }
	internal PhysicalRigBodyAssembly Bodies { get; }
	internal PhysicalRigTargetAssembly Targets { get; }
	internal PhysicalJointState[] CanonicalJoints { get; }
	internal PhysicalJointState[] OutputJoints { get; }
	internal PhysicalStructuralJoint[] StructuralJoints { get; }
	internal PhysicalJointPreset[] Presets { get; }

	internal PhysicalRigConstruction( EffectorRigAsset asset, LimbRigidTransform[] clipPoses,
		bool compound, LimbSolverMath math, Vector3 defaultAcceleration, int accumulatorState )
	{
		ArgumentNullException.ThrowIfNull( asset );
		ArgumentNullException.ThrowIfNull( clipPoses );
		ArgumentNullException.ThrowIfNull( math );
		if ( asset.PhysicalSettings is not { } settings || asset.Structural is null )
			throw new ArgumentException( "Physical rig construction requires authored body and structural settings." );
		Hierarchy = asset.InitializeHierarchy( clipPoses );
		Effectors = asset.CreateState( Hierarchy );
		Presets = settings.CreateDrivePresets();
		CanonicalJoints = settings.CreateCanonicalJoints( Hierarchy );
		OutputJoints = CanonicalJoints.AsSpan( 1, 22 ).ToArray();
		StructuralJoints = settings.BuildAuthoredJoints( asset.Structural, Hierarchy, math );
		Bodies = settings.CreateBodies( compound, math, defaultAcceleration );
		Targets = settings.CreateTargets( defaultAcceleration, accumulatorState );
	}

	internal PhysicalRigState CreateState( EffectorPoseCache cache,
		PhysicalPosePreparationState preparation, PhysicalPoseResetState reset ) =>
		new( Effectors, cache, preparation, reset, Targets.Bindings, Bodies.Bindings, OutputJoints, Presets );
}
