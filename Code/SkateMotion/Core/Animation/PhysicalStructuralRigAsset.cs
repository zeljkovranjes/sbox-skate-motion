namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Validated authored structural data connected to the rig's canonical hierarchy.</summary>
internal sealed class PhysicalStructuralRigAsset
{
	private readonly PhysicalStructuralBone[] _bones;
	private readonly int[] _solverToClip;

	internal PhysicalStructuralRigAsset( PhysicalStructuralRigDefinition definition,
		EffectorRigBoneDefinition[] bindings, int[] solverToClip )
	{
		ArgumentNullException.ThrowIfNull( definition );
		ArgumentNullException.ThrowIfNull( bindings );
		ArgumentNullException.ThrowIfNull( solverToClip );
		if ( definition.Version != 1 || definition.Bones is null || definition.Bones.Length != 24
			|| bindings.Length != 24 || solverToClip.Length != 24 )
			throw new ArgumentException( "Structural metadata requires version 1 and 24 matching canonical bones." );
		_bones = new PhysicalStructuralBone[ 24 ];
		_solverToClip = solverToClip.AsSpan().ToArray();
		for ( var i = 0; i < _bones.Length; ++i )
		{
			var bone = definition.Bones[ i ] ?? throw new ArgumentException( "Structural bone metadata is missing." );
			var bind = bindings[ i ] ?? throw new ArgumentException( "Structural bind metadata is missing." );
			if ( string.IsNullOrEmpty( bone.ClipBone ) || !string.Equals( bone.ClipBone, bind.ClipBone, StringComparison.OrdinalIgnoreCase ) )
				throw new ArgumentException( "Structural bones must match the canonical bind order." );
			if ( !float.IsFinite( bone.SwingAngle ) || !float.IsFinite( bone.TwistAngle ) )
				throw new ArgumentException( "Structural angular ranges must be finite." );
			Validate( bind.BindPosition, 3 );
			_bones[ i ] = new( Rotation( bone.ParentToBoneRotation ), Rotation( bone.JointRotation ),
				Rotation( bind.BindRotation ), new Vector3( bind.BindPosition[ 0 ], bind.BindPosition[ 1 ], bind.BindPosition[ 2 ] ),
				bone.SwingAngle, bone.TwistAngle );
		}
	}

	internal PhysicalStructuralJoint[] Build( EffectorSkeletonHierarchy hierarchy,
		PhysicalStructuralJointOverride[] overrides, PhysicalStructuralJointTuning tuning, LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( hierarchy );
		if ( !hierarchy.SolverToClip.AsSpan().SequenceEqual( _solverToClip ) )
			throw new ArgumentException( "Structural construction requires this asset's canonical mapping." );
		return PhysicalStructuralJointInitialization.Build( _bones, hierarchy.InitialBones,
			hierarchy.Parents, hierarchy.SolverToClip, hierarchy.ClipToSolver, overrides, tuning, math );
	}

	private static Vector4 Rotation( float[] values )
	{
		Validate( values, 4 );
		return new( values[ 0 ], values[ 1 ], values[ 2 ], values[ 3 ] );
	}

	private static void Validate( float[] values, int count )
	{
		if ( values is null || values.Length != count )
			throw new ArgumentException( "Structural transform component count is invalid." );
		foreach ( var value in values )
		{
			if ( !float.IsFinite( value ) )
				throw new ArgumentException( "Structural transform components must be finite." );
		}
	}
}
