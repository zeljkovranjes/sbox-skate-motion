namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Runs mode, weight, pose, contact and bone updates over one persistent rig.</summary>
internal sealed class EffectorRigController
{
	private readonly EffectorWeightController _weights;
	private readonly EffectorPosePreparer _preparer;
	private readonly EffectorPoseBlender _blender;
	private readonly FootReachController _feet;
	private readonly EffectorRigSolver _bones;
	private readonly Vector3 _bounds;
	private readonly float[] _pinnedWeights = new float[ 4 ];
	private readonly Vector3[] _offsets = new Vector3[ 4 ];
	private readonly Vector3[] _handPositions = new Vector3[ 2 ];
	private readonly EffectorPoseInput[] _poseInputs = new EffectorPoseInput[ 4 ];

	internal EffectorRigController( EffectorWeightController weights, EffectorPosePreparer preparer,
		EffectorPoseBlender blender, FootReachController feet, EffectorRigSolver bones, Vector3 bounds )
	{
		ArgumentNullException.ThrowIfNull( weights );
		ArgumentNullException.ThrowIfNull( preparer );
		ArgumentNullException.ThrowIfNull( blender );
		ArgumentNullException.ThrowIfNull( feet );
		ArgumentNullException.ThrowIfNull( bones );
		_weights = weights;
		_preparer = preparer;
		_blender = blender;
		_feet = feet;
		_bones = bones;
		_bounds = bounds;
	}

	internal void Update( EffectorRigState state, EffectorRigFrame frame, EffectorRigInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( input.Poses );
		ArgumentNullException.ThrowIfNull( input.Pinning );
		ArgumentNullException.ThrowIfNull( input.Releasing );
		ArgumentNullException.ThrowIfNull( input.Contacts );
		if ( input.Poses.Length != 4 || input.Pinning.Length != 4 || input.Releasing.Length != 4 || input.Contacts.Length != 2 )
			throw new ArgumentException( "Rig input needs four pose/request slots and two contacts." );
		for ( var i = 0; i < 4; i++ )
		{
			_pinnedWeights[ i ] = state.Poses[ i ].TargetState.Weight;
			_offsets[ i ] = state.Poses[ i ].TargetState.Offset;
		}
		// Weighting consumes the previous preparation's hand positions.
		_handPositions[ 0 ] = state.Poses[ 2 ].LocalPosition;
		_handPositions[ 1 ] = state.Poses[ 3 ].LocalPosition;
		EffectorModeController.Update( state.Weights, input.Pinning, input.Releasing, _pinnedWeights, _offsets,
			input.FeetEnabled, input.LeftHandEnabled, input.RightHandEnabled );
		_weights.Update( state.Weights, _pinnedWeights, input.Releasing, _handPositions, frame.WeightAnchor, input.DisableFeet );
		for ( var i = 0; i < 4; i++ )
		{
			var pose = state.Poses[ i ];
			var weight = state.Weights[ i ];
			state.Poses[ i ] = pose with { TargetState = pose.TargetState with { Mode = weight.Mode, Offset = _offsets[ i ] } };
			_poseInputs[ i ] = input.Poses[ i ] with { TargetWeight = weight.Target, OverrideWeight = weight.Override };
		}
		_preparer.Update( _poseInputs, state.Poses, frame.Root, frame.ModelToWorld, frame.WorldToModel, _bounds );
		_blender.Update( _poseInputs, state.Poses, frame.Root, frame.ModelToWorld, frame.AuthoredOrientation );
		state.FootFlags = _feet.Update( input.Contacts, state.Feet, state.Poses, input.SelectedContactId,
			frame.ContactFrame, frame.OffsetOrientation, frame.ReferenceHeight, input.ReactionEnabled,
			input.RequestSuppressed, state.FootFlags );
		var skeleton = state.Skeleton;
		_bones.Update( skeleton.Bindings, state.Poses, skeleton.Parents, skeleton.BindTransforms,
			skeleton.AnimatedBones, skeleton.MutableBones, frame.WorldToModel, input.MinimumDegrees, input.MaximumDegrees );
	}
}
