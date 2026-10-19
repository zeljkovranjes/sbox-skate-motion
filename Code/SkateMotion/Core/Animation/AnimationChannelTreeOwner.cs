namespace SkateMotion.Core.Animation;

/// <summary>Retains current and pending overlay trees while reusing sequence and transition nodes.</summary>
internal sealed class AnimationChannelTreeOwner : IAnimationPlaybackNode
{
	private readonly IAnimationChannelTreeFactory _factory;
	private readonly AnimationChannelBlend _blend;
	private readonly AnimationSequencePlayback _sequence;
	private readonly AnimationTransitionPlayback _transition;
	private readonly AnimationSequencePosePlayback _poseSequence;
	private readonly AnimationTransitionPosePlayback _poseTransition;
	private IAnimationPlaybackTree _currentTree;
	private IAnimationPlaybackTree _pendingTree;
	private bool _pendingTransition;

	internal AnimationChannelTreeOwner( IAnimationPlaybackTree currentTree,
		IAnimationChannelTreeFactory factory, AnimationChannelBlend blend )
		: this( currentTree, factory, blend, false )
	{
	}

	internal AnimationChannelTreeOwner( IAnimationPlaybackTree currentTree,
		IAnimationChannelTreeFactory factory, AnimationChannelBlend blend, bool preparePoses )
	{
		_currentTree = currentTree;
		_factory = factory;
		_blend = blend;
		Root = currentTree.Root;
		if ( preparePoses )
		{
			if ( Root is not IAnimationPosePlaybackNode poseRoot )
				throw new ArgumentException( "The current tree must support body poses and auxiliary streams.", nameof( currentTree ) );
			_poseSequence = new AnimationSequencePosePlayback( poseRoot, poseRoot );
			_poseTransition = new AnimationTransitionPosePlayback( poseRoot, poseRoot, 0f, AnimationTransitionMode.Both, 0 );
			_sequence = _poseSequence.Playback;
			_transition = _poseTransition.Playback;
		}
		else
		{
			_sequence = new AnimationSequencePlayback( Root, Root );
			_transition = new AnimationTransitionPlayback( Root, Root, 0f, AnimationTransitionMode.Both );
		}
	}

	internal IAnimationPlaybackNode Root { get; private set; }
	internal bool HasPending => _pendingTree is not null;

	/// <summary>Reuses the channel's composite nodes after its storage has been released.</summary>
	internal void InitializeReleased( IAnimationPlaybackTree tree )
	{
		ArgumentNullException.ThrowIfNull( tree );
		if ( _currentTree is not null || _pendingTree is not null )
			throw new InvalidOperationException( "Channel storage must be released before initialization." );
		_currentTree = tree;
		Root = tree.Root;
	}

	/// <summary>Releases current storage before pending storage while retaining the published node during callbacks.</summary>
	internal void ReleaseTrees()
	{
		if ( _currentTree is not null )
			_factory.Release( _currentTree );
		if ( _pendingTree is not null )
			_factory.Release( _pendingTree );
		_currentTree = null;
		_pendingTree = null;
	}

	/// <summary>Clears channel ownership before the global storage reset releases all registered trees.</summary>
	internal void ForgetTrees()
	{
		_currentTree = null;
		_pendingTree = null;
	}

	/// <summary>Reinitializes reusable storage for a new channel after its prior envelope has ended.</summary>
	internal void Restart( IAnimationPlaybackTree tree )
	{
		ArgumentNullException.ThrowIfNull( tree );
		if ( _poseSequence is not null && tree.Root is not IAnimationPosePlaybackNode )
			throw new ArgumentException( "The tree must support pose preparation.", nameof( tree ) );
		Root = tree.Root;
		if ( _pendingTree is not null )
			_factory.Release( _pendingTree );
		_factory.Release( _currentTree );
		_currentTree = tree;
		_pendingTree = null;
		_factory.Commit( tree );
	}

	/// <summary>The current composite or promoted overlay duration.</summary>
	public float Duration => Root.Duration;

	/// <summary>The current composite or promoted overlay clock.</summary>
	public float Elapsed => Root.Elapsed;

	internal AnimationChannelReplaceResult Replace( AnimationChannelReplacement request )
	{
		if ( !_blend.CanReplace( request.AllowRecovery ) )
			return AnimationChannelReplaceResult.FadingOut;
		if ( HasPending )
			PromotePending();

		var tree = _factory.Create( request );
		if ( tree is null )
			return AnimationChannelReplaceResult.MissingTree;
		if ( _poseSequence is not null && tree.Root is not IAnimationPosePlaybackNode )
			throw new InvalidOperationException( "The replacement factory must return body and auxiliary pose trees." );

		IAnimationPlaybackNode replacement;
		if ( request.Transition )
		{
			if ( _poseTransition is not null )
			{
				_poseTransition.Reset( (IAnimationPosePlaybackNode)Root, (IAnimationPosePlaybackNode)tree.Root,
					request.BlendDuration, AnimationTransitionMode.Both );
				replacement = _poseTransition;
			}
			else
			{
				_transition.Reset( Root, tree.Root, request.BlendDuration, AnimationTransitionMode.Both );
				replacement = _transition;
			}
		}
		else
		{
			if ( _poseSequence is not null )
			{
				_poseSequence.Reset( (IAnimationPosePlaybackNode)Root, (IAnimationPosePlaybackNode)tree.Root );
				replacement = _poseSequence;
			}
			else
			{
				_sequence.Reset( Root, tree.Root );
				replacement = _sequence;
			}
		}
		_pendingTree = tree;
		_pendingTransition = request.Transition;
		_blend.ApplyReplacementEnvelope( request.RecoveryDuration, request.ExitDuration,
			request.Flags, request.AllowRecovery );
		Root = replacement;
		_factory.Commit( tree );
		return AnimationChannelReplaceResult.Success;
	}

	/// <summary>Runs the pending cleanup pass separately from playback and envelope updates.</summary>
	internal bool CleanupCompleted()
	{
		if ( !HasPending || !(_pendingTransition ? _transition.CanReleasePrevious : _sequence.ActiveNext) )
			return false;
		PromotePending();
		return true;
	}

	private void PromotePending()
	{
		Root = _pendingTree.Root;
		// Destruction observes the promoted successor while the old tree is still retained.
		_factory.Release( _currentTree );
		_currentTree = _pendingTree;
		_pendingTree = null;
	}

	/// <summary>Advances the selected overlay without implicitly running tree cleanup.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
		=> Root.Advance( delta, phase, ref completion );

	/// <summary>Seeks the current overlay or composite successor.</summary>
	public void Seek( float time ) => Root.Seek( time );
}
