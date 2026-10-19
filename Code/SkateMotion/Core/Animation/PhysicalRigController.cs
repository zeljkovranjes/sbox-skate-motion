namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Coordinates preparation, conditional reset and rig solving, joint output and correction.</summary>
internal sealed class PhysicalRigController
{
	private const float CorrectionTimeStep = .01666666753590107f;
	private readonly EffectorRigController _rig;
	private readonly EffectorPoseInput[] _poseInputs = new EffectorPoseInput[ 4 ];

	internal PhysicalRigController( EffectorRigController rig )
	{
		ArgumentNullException.ThrowIfNull( rig );
		_rig = rig;
	}

	internal void Update( PhysicalRigState state, PhysicalRigInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( state.FeedbackState is { } feedback )
			input = feedback.BindInput( input );
		var position = input.Frames.ModelToWorld.Translation;
		state.Velocity = Scale( position - state.PreviousPosition, Reciprocal( input.DeltaTime ) );
		state.PreviousPosition = position;

		var preparation = state.Preparation;
		preparation.ModelToWorld = input.Frames.ModelToWorld;
		preparation.SolverFrame = input.Frames.Solver;
		preparation.ContactFrame = input.Frames.Contact;
		if ( PhysicalPosePreparation.Update( preparation, state.Targets, state.Feedback ) == 0 )
		{
			PhysicalPoseReset.Update( state.Reset, preparation, state.Cache, state.Bodies,
				state.Rig.Skeleton.MutableBones, preparation.ModelToWorld );
			if ( input.MarkReset )
				state.ResetFlag = 1;
		}

		if ( input.SolveRig )
			Solve( state, input );
		state.RootDrive = PhysicalPoseOutput.Update( state.Rig.Skeleton.MutableBones, state.Inverses,
			state.Joints, input.JointMask, input.JointSettings, state.Presets );
		var correction = Scale( new Vector3( 0f, -input.Gravity, 0f ), .5f );
		correction = Scale( Scale( correction, CorrectionTimeStep ), CorrectionTimeStep );
		PhysicalSkeletonCorrection.Apply( state.Bodies, correction );
	}

	internal void UpdateWithFeedbackTargets( PhysicalRigState state, PhysicalRigInput input, float requestedHeight )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Preparation.ModelToWorld = input.Frames.ModelToWorld;
		PhysicalFeedbackFrames.Update( state.Preparation, state.Reset.WorldReference, requestedHeight );
		Update( state, input );
	}

	private void Solve( PhysicalRigState state, PhysicalRigInput input )
	{
		var cache = state.Cache.Bones;
		state.Frames = EffectorRigFrameBinder.Bind( input.Frames with { AnimatedRoot = cache[ 0 ] }, input.BindAnimatedRoot );
		var frames = state.Frames;
		var rigInput = input.RigInput with { Poses = _poseInputs };
		for ( var i = 0; i < state.Rig.Skeleton.Bindings.Length; i++ )
		{
			var binding = state.Rig.Skeleton.Bindings[ i ];
			// Target preparation reads retained poses; bone application reads raw animation separately.
			_poseInputs[ i ] = input.RigInput.Poses[ i ] with
			{
				Animated = cache[ binding.EndBone ],
				Reference = binding.ReferenceBone < 0 ? null : cache[ binding.ReferenceBone ]
			};
		}
		var frame = new EffectorRigFrame( cache[ 0 ], frames.ModelToWorld, frames.WorldToModel,
			frames.Authored.Rotation, frames.Contact, frames.Offset.Rotation,
			frames.ReferencePosition.Y, state.WeightAnchor );
		_rig.Update( state.Rig, frame, rigInput );
	}
}
