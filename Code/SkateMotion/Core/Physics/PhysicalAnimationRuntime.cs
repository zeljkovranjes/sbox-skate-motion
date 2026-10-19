using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Owns the live animation-to-drive, solve, readback and clip-pose sequence for one rider.</summary>
internal sealed class PhysicalAnimationRuntime
{
	internal const float Step = .01666666753590107f;
	private readonly EffectorRigAsset _asset;
	private readonly ClipSkeleton _clips;
	private readonly PhysicalAnimationRuntimeSettings _settings;
	private readonly PhysicalRigConstruction _construction;
	private readonly PhysicalRigSolverBinding _drives;
	private readonly PhysicalStructuralRigBinding _joints;
	private readonly PhysicalRigContactBinding _contacts;
	private readonly LimbRigidTransform[] _clipModels;
	private readonly LimbRigidTransform[] _rigModels;
	private readonly LimbRigidTransform[] _renderModels;
	private readonly LimbRigidTransform[] _previousSolved = new LimbRigidTransform[ 24 ];
	private readonly byte[] _jointMask = new byte[ 24 ];
	private readonly float[] _weights = new float[ 24 ];
	private readonly DriveConstraint[] _driveConstraints = new DriveConstraint[ 48 ];
	private readonly JointConstraint[] _jointConstraints = new JointConstraint[ 22 ];
	private float _remaining;
	private bool _initialized;
	private LimbRigidTransform _lastFrame;

	internal PhysicalRigState State { get; }
	internal PhysicalSolverWorld World { get; }
	internal PhysicalContactOwner ContactOwner => _contacts.Owner;
	internal long SolvedSteps { get; private set; }
	internal int LastDriveCount { get; private set; }
	internal int LastJointCount { get; private set; }

	internal PhysicalAnimationRuntime( EffectorRigAsset asset, ClipSkeleton clips, PhysicalAnimationRuntimeSettings settings )
	{
		_asset = asset ?? throw new ArgumentNullException( nameof( asset ) );
		_clips = clips ?? throw new ArgumentNullException( nameof( clips ) );
		_settings = settings ?? throw new ArgumentNullException( nameof( settings ) );
		_clipModels = new LimbRigidTransform[ clips.Count ];
		_rigModels = new LimbRigidTransform[ clips.Count ];
		_renderModels = new LimbRigidTransform[ clips.Count ];
		var rest = new SkeletonPose( clips.Count );
		rest.SetRest( clips );
		Gather( rest );
		var gravity = new Vector3( 0f, -settings.Gravity, 0f );
		_construction = new( asset, _clipModels, false, settings.CreateMath(), gravity, 0 );
		State = _construction.CreateState( new( 24 ), new(), new( 26 ) );
		var bodies = new PhysicalBodyState[ 30 ];
		var dynamics = new BodyDynamicsSettings[ 30 ];
		var integration = new int[ 26 ];
		for ( var i = 0; i < bodies.Length; i++ )
		{
			bodies[ i ] = i < 26 ? State.Bodies[ i ].Body : State.Targets[ i - 26 ].Body;
			if ( i >= 26 )
				continue;
			integration[ i ] = i;
			var mode = _construction.Bodies.ModeBodies[ i ];
			dynamics[ i ] = new( bodies[ i ].Mass.Value.InverseInertia, mode.LinearDamping, mode.AngularDamping,
				settings.MaximumSpeed, settings.MaximumAngularSpeed, 1f, 0f, 0 );
		}
		World = new( bodies, dynamics, integration, 48, 22 );
		_contacts = new( World, State.Bodies, new() );
		_joints = new( World, State.Bodies, _construction.StructuralJoints );
		_drives = new( World, State, _construction.CanonicalJoints, _construction.Hierarchy.InitialBones, _construction.Hierarchy.Parents );
		for ( var i = 0; i < 24; i++ )
			_weights[ i ] = asset.PhysicalSettings.Bones[ i ].FeedbackWeight;
	}

	internal void Invalidate() => _initialized = false;

	internal void Update( SkeletonPose pose, LimbRigidTransform modelToWorld, float delta )
	{
		if ( !float.IsFinite( delta ) || delta < 0f )
			throw new ArgumentOutOfRangeException( nameof( delta ) );
		Gather( pose );
		var skeleton = State.Rig.Skeleton;
		EffectorPoseGatherer.GatherBones( _asset.SolverToClip, skeleton.BindTransforms,
			_rigModels, skeleton.AnimatedBones, skeleton.MutableBones );
		State.Cache.Update( skeleton.MutableBones, _weights, modelToWorld );
		SetTargets( modelToWorld );
		if ( !_initialized || delta > .25f || Vector3.DistanceSquared( modelToWorld.Translation, _lastFrame.Translation ) > 4f )
		{
			PhysicalPoseReset.Update( State.Reset, State.Preparation, State.Cache, State.Bodies, skeleton.MutableBones, modelToWorld );
			for ( var i = 0; i < 24; i++ )
				_previousSolved[ i ] = State.Reset.Poses[ i ];
			_remaining = 0f;
			_initialized = true;
		}
		_lastFrame = modelToWorld;
		_remaining += MathF.Min( delta, .25f );
		while ( _remaining >= Step )
		{
			for ( var i = 0; i < 24; i++ )
				_previousSolved[ i ] = State.Bodies[ i ].Read();
			State.Inverses[ 0 ] = skeleton.MutableBones[ 0 ].RigidInverse();
			State.RootDrive = PhysicalPoseOutput.Update( skeleton.MutableBones, State.Inverses,
				State.Joints, _jointMask, new( 1f, true, 1f, 1f ), State.Presets );
			_drives.RefreshRootDrive();
			World.Prepare();
			LastDriveCount = World.CompilePoseAttachments( _driveConstraints, Step );
			LastJointCount = World.CompileStructuralJoints( _jointConstraints, Step );
			World.SolveAndIntegrate( [], _jointConstraints.AsSpan( 0, LastJointCount ),
				_driveConstraints.AsSpan( 0, LastDriveCount ), _settings.SolverPasses, Step, new( 0f, -_settings.Gravity, 0f ) );
			PhysicalPoseReadback.Update( State.Reset, State.Bodies, State.Targets[ 1 ].Read(), _weights );
			PhysicalPoseReadback.UpdateErrors( State.Reset, State.Preparation, modelToWorld, skeleton.MutableBones );
			_remaining -= Step;
			SolvedSteps++;
		}
		WritePose( pose, modelToWorld, _remaining / Step );
	}

	private void SetTargets( LimbRigidTransform frame )
	{
		var mutable = State.Rig.Skeleton.MutableBones;
		var root = frame.Compose( mutable[ 23 ] );
		var board = frame.Compose( mutable[ 0 ] );
		State.Preparation.ModelToWorld = frame;
		State.Preparation.FirstFeedbackFrame = root;
		State.Preparation.SecondFeedbackFrame = root;
		State.Preparation.FirstFeedbackPosition = root.Translation;
		State.Preparation.SecondFeedbackPosition = root.Translation;
		State.Targets[ 0 ].Write( root );
		State.Targets[ 1 ].Write( board );
		State.Targets[ 2 ].Write( root );
		State.Targets[ 3 ].Write( root );
	}

	private void Gather( SkeletonPose pose )
	{
		for ( var i = 0; i < _clips.Count; i++ )
		{
			var local = FromPose( pose.Positions[ i ], pose.Rotations[ i ] );
			var parent = _clips.Parents[ i ];
			// The gameplay motor owns travel. Match the retargeter's root-relative pose so
			// a looping clip's accumulated trajectory is not applied to physical bodies twice.
			_clipModels[ i ] = parent < 0 ? LimbRigidTransform.Identity : _clipModels[ parent ].Compose( local );
			_rigModels[ i ] = _asset.Space.ToRig( _clipModels[ i ] );
		}
	}

	private void WritePose( SkeletonPose pose, LimbRigidTransform frame, float blend )
	{
		var inverse = frame.RigidInverse();
		var hierarchy = _construction.Hierarchy;
		for ( var i = 0; i < _clips.Count; i++ )
		{
			var canonical = hierarchy.ClipToSolver[ i ];
			var parent = _clips.Parents[ i ];
			if ( canonical < 0 || canonical == 0 )
			{
				var local = FromPose( pose.Positions[ i ], pose.Rotations[ i ] );
				_renderModels[ i ] = parent < 0 ? LimbRigidTransform.Identity : _renderModels[ parent ].Compose( local );
				continue;
			}
			var current = State.Bodies[ canonical ].Read();
			var previous = _previousSolved[ canonical ];
			var interpolated = FromPose( Vector3.Lerp( previous.Translation, current.Translation, blend ),
				Quaternion.Slerp( Rotation( previous ), Rotation( current ), blend ) );
			var model = inverse.Compose( interpolated ).Compose( hierarchy.InverseBindTransforms[ canonical ] );
			_renderModels[ i ] = _asset.Space.ToClip( model );
			var output = parent < 0 ? _renderModels[ i ] : _renderModels[ parent ].RigidInverse().Compose( _renderModels[ i ] );
			pose.Positions[ i ] = output.Translation;
			pose.Rotations[ i ] = Rotation( output );
		}
	}

	private static Quaternion Rotation( LimbRigidTransform pose )
	{
		var q = PhysicalPoseOutput.Rotation( pose.Rotation );
		return Quaternion.Normalize( new( q.X, q.Y, q.Z, q.W ) );
	}

	internal static LimbRigidTransform FromPose( Vector3 position, Quaternion rotation ) => new(
		new LimbFrame( Vector3.Transform( Vector3.UnitX, rotation ), Vector3.Transform( Vector3.UnitY, rotation ),
			Vector3.Transform( Vector3.UnitZ, rotation ) ), position );

	internal void Release()
	{
		_drives.Release();
		_joints.Release();
		_contacts.Release();
	}
}
