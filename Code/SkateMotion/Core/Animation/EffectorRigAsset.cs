namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Validated bind metadata shared by riders, with explicit clip-to-rig conversion.</summary>
internal sealed class EffectorRigAsset
{
	internal RigCoordinateSpace Space { get; }
	internal int[] SolverToClip { get; }
	internal Quaternion[] BindRotations { get; }
	internal Vector3[] BindPositions { get; }
	internal Vector3[] BodySizes { get; }
	internal PhysicalStructuralRigAsset Structural { get; }
	internal PhysicalRigSettingsAsset PhysicalSettings { get; }
	internal int[] HelperClipBones { get; } = new int[ 4 ];
	internal int[] EndBones { get; } = new int[ 4 ];
	internal int[] ReferenceBones { get; } = new int[ 4 ];
	private readonly int[] _clipParents;

	internal EffectorRigAsset( EffectorRigDefinition definition, ClipSkeleton clips )
	{
		ArgumentNullException.ThrowIfNull( definition );
		ArgumentNullException.ThrowIfNull( clips );
		if ( definition.Version != 1 || definition.Bones is null || definition.Bones.Length == 0
			|| definition.Effectors is null || definition.Effectors.Length != 4 )
			throw new ArgumentException( "Rig metadata requires version 1, solver bones and four effector slots." );
		Space = new RigCoordinateSpace( definition.ClipAxes );
		var count = definition.Bones.Length;
		SolverToClip = new int[ count ];
		BindRotations = new Quaternion[ count ];
		BindPositions = new Vector3[ count ];
		BodySizes = new Vector3[ count ];
		_clipParents = new int[ clips.Count ];
		Array.Copy( clips.Parents, _clipParents, clips.Count );
		for ( var i = 0; i < count; i++ )
		{
			var bone = definition.Bones[ i ] ?? throw new ArgumentException( "Rig bone metadata is missing." );
			var clip = ClipIndex( clips, bone.ClipBone );
			for ( var previous = 0; previous < i; previous++ )
			{
				if ( SolverToClip[ previous ] == clip )
					throw new ArgumentException( "Each solver bone must map to a distinct clip bone." );
			}
			SolverToClip[ i ] = clip;
			ValidateNumbers( bone.BindRotation, 4 );
			BindRotations[ i ] = new Quaternion( bone.BindRotation[ 0 ], bone.BindRotation[ 1 ], bone.BindRotation[ 2 ], bone.BindRotation[ 3 ] );
			BindPositions[ i ] = Vector( bone.BindPosition );
			BodySizes[ i ] = Vector( bone.BodySize );
			if ( BodySizes[ i ].X < 0f || BodySizes[ i ].Y < 0f || BodySizes[ i ].Z < 0f )
				throw new ArgumentException( "Body dimensions must be nonnegative." );
		}
		for ( var i = 0; i < 4; i++ )
		{
			var slot = definition.Effectors[ i ] ?? throw new ArgumentException( "Rig effector metadata is missing." );
			HelperClipBones[ i ] = ClipIndex( clips, slot.HelperBone );
			EndBones[ i ] = SolverIndex( clips, slot.EndBone );
			ReferenceBones[ i ] = string.IsNullOrEmpty( slot.ReferenceBone ) ? -1 : SolverIndex( clips, slot.ReferenceBone );
		}
		Structural = definition.PhysicalJoints is null ? null
			: new PhysicalStructuralRigAsset( definition.PhysicalJoints, definition.Bones, SolverToClip );
		PhysicalSettings = definition.PhysicalSettings is null ? null
			: new PhysicalRigSettingsAsset( definition.PhysicalSettings, definition.Bones, BodySizes );
	}

	/// <summary>Initializes from model-space clip matrices, not individual bone-local transforms.</summary>
	internal EffectorSkeletonHierarchy InitializeHierarchy( LimbRigidTransform[] clipPoses )
	{
		if ( clipPoses.Length != _clipParents.Length )
			throw new ArgumentException( "Clip poses must match the bound skeleton." );
		var rigPoses = new LimbRigidTransform[ clipPoses.Length ];
		for ( var i = 0; i < rigPoses.Length; i++ )
			rigPoses[ i ] = Space.ToRig( clipPoses[ i ] );
		return EffectorSkeletonInitializer.Create( _clipParents, SolverToClip, BindRotations, BindPositions, rigPoses );
	}

	internal EffectorRigState CreateState( EffectorSkeletonHierarchy hierarchy )
	{
		ArgumentNullException.ThrowIfNull( hierarchy );
		if ( !hierarchy.SolverToClip.AsSpan().SequenceEqual( SolverToClip ) )
			throw new ArgumentException( "Effector construction requires this asset's canonical mapping." );
		var bindings = new EffectorBoneBinding[ 4 ];
		var poses = new EffectorPoseState[ 4 ];
		var identity = LimbRigidTransform.Identity;
		for ( var i = 0; i < bindings.Length; ++i )
		{
			bindings[ i ] = new( EndBones[ i ], ReferenceBones[ i ], i < 2 ? .8f : 1f );
			poses[ i ] = new( default, identity, identity, identity, identity, identity, identity, Vector3.Zero, false );
		}
		var animated = hierarchy.InitialBones.AsSpan().ToArray();
		var mutable = new LimbRigidTransform[ animated.Length ];
		for ( var i = 0; i < mutable.Length; ++i )
			mutable[ i ] = animated[ i ].Compose( hierarchy.BindTransforms[ i ] );
		var skeleton = new EffectorRigSkeleton( bindings, hierarchy.Parents.AsSpan().ToArray(),
			hierarchy.BindTransforms.AsSpan().ToArray(), animated, mutable );
		return new( new EffectorWeightState[ 4 ], poses, new FootReachState[ 2 ], skeleton, default );
	}

	private int SolverIndex( ClipSkeleton clips, string name )
	{
		var clip = ClipIndex( clips, name );
		for ( var i = 0; i < SolverToClip.Length; i++ )
		{
			if ( SolverToClip[ i ] == clip )
				return i;
		}
		throw new ArgumentException( $"Effector bone '{name}' is not in solver order." );
	}

	private static int ClipIndex( ClipSkeleton clips, string name )
	{
		var index = string.IsNullOrEmpty( name ) ? -1 : clips.IndexOf( name );
		return index >= 0 ? index : throw new ArgumentException( $"Rig clip bone '{name}' is missing." );
	}

	private static Vector3 Vector( float[] values )
	{
		ValidateNumbers( values, 3 );
		return new Vector3( values[ 0 ], values[ 1 ], values[ 2 ] );
	}

	private static void ValidateNumbers( float[] values, int count )
	{
		if ( values is null || values.Length != count )
			throw new ArgumentException( "Rig transforms have an invalid component count." );
		foreach ( var value in values )
		{
			if ( !float.IsFinite( value ) )
				throw new ArgumentException( "Rig transform components must be finite." );
		}
	}
}
