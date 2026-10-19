namespace SkateMotion.Core.Animation;

/// <summary>Owns bounded base trees and splices retained wrappers without resetting their clocks.</summary>
internal sealed class AnimationBasePoseTreeOwner : IAnimationPosePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly IAnimationBasePoseTreeFactory _factory;
	private readonly Action<IAnimationPosePlaybackNode> _publish;
	private readonly IAnimationPlaybackTree[] _trees = new IAnimationPlaybackTree[ 4 ];
	private readonly Wrapper[] _sequences = new Wrapper[ 3 ];
	private readonly Wrapper[] _transitions = new Wrapper[ 3 ];
	private readonly Wrapper[] _retained = new Wrapper[ 3 ];
	private readonly Wrapper[] _retainedTransitions = new Wrapper[ 3 ];
	private readonly int[] _freeTrees = new int[ 4 ];
	private readonly int[] _freeSequences = new int[ 3 ];
	private readonly int[] _freeTransitions = new int[ 3 ];
	private int _freeTreeCount;
	private int _freeSequenceCount;
	private int _freeTransitionCount;
	private int _transitionCount;
	private int _retainedCount;

	internal AnimationBasePoseTreeOwner( IAnimationBasePoseTreeFactory factory )
		: this( factory, null )
	{
	}

	internal AnimationBasePoseTreeOwner( IAnimationBasePoseTreeFactory factory,
		Action<IAnimationPosePlaybackNode> publish )
	{
		ArgumentNullException.ThrowIfNull( factory );
		_factory = factory;
		_publish = publish;
		for ( var i = 0; i < 3; i++ )
		{
			_sequences[ i ] = new Wrapper( i, false );
			_transitions[ i ] = new Wrapper( i, true );
		}
		ResetPools();
	}

	internal IAnimationPosePlaybackNode Root { get; private set; }
	internal int RetainedCount => _retainedCount;
	internal int RetainedTransitionCount => _transitionCount;
	internal AnimationPlaybackCompletion Completion { get; private set; } = AnimationPlaybackCompletion.Empty;
	internal bool ReplacementPending { get; private set; }
	internal bool ReplacementAdvanced { get; private set; }

	/// <summary>The currently published base pose.</summary>
	public SkeletonPose Pose => Root.Pose;
	/// <summary>The published tree's successor duration.</summary>
	public float Duration => Root.Duration;
	/// <summary>The published tree's successor clock.</summary>
	public float Elapsed => Root.Elapsed;

	internal bool Replace( AnimationBasePoseRequest request )
	{
		if ( request.Transition is not (AnimationBaseTransition.Play or AnimationBaseTransition.Blend
			or AnimationBaseTransition.Sequence or AnimationBaseTransition.ChannelBlend) )
			throw new ArgumentOutOfRangeException( nameof( request ) );
		var immediate = request.Transition == AnimationBaseTransition.Play || Root is null;
		if ( immediate )
			ResetPools();
		else
		{
			if ( request.Transition != AnimationBaseTransition.Sequence && _transitionCount == 3 )
				RemoveWrapper( _retainedTransitions[ 0 ] );
			if ( _retainedCount == 3 )
				RemoveWrapper( _retained[ 0 ] );
		}

		var slot = _freeTrees[ --_freeTreeCount ];
		var tree = _factory.Create( slot, request.Definition );
		_trees[ slot ] = tree;
		if ( tree is null )
		{
			// A failed construction resets storage accounting but does not publish a replacement root.
			ResetPools();
			MarkReplacement();
			return false;
		}
		if ( tree.Root is not IAnimationPosePlaybackNode next || tree.Root is not IAnimationRatePlaybackNode rated )
			throw new InvalidOperationException( "Base trees must support pose preparation and playback rate changes." );
		rated.SetRate( request.Rate );
		var startTime = Root is null && request.Transition != AnimationBaseTransition.Play ? 0f : request.StartTime;
		if ( startTime > 0f )
			next.Seek( startTime );
		if ( immediate )
		{
			Publish( next );
			MarkReplacement();
			return true;
		}

		var parent = request.Under && _retainedCount != 0 ? _retained[ _retainedCount - 1 ] : null;
		var previous = parent is null ? Root : parent.Next.Target;
		var wrapper = request.Transition == AnimationBaseTransition.Sequence
			? _sequences[ _freeSequences[ --_freeSequenceCount ] ]
			: _transitions[ _freeTransitions[ --_freeTransitionCount ] ];
		_retained[ _retainedCount++ ] = wrapper;
		if ( wrapper.IsTransition )
			_retainedTransitions[ _transitionCount++ ] = wrapper;
		var blendMode = request.Transition == AnimationBaseTransition.ChannelBlend
			? int.MinValue | (request.UseChannelFromWeights ? 0x40000000 : 0)
			: 0;
		wrapper.Reset( previous, next, request.Duration, request.Synchronization, blendMode );
		if ( parent is null )
			Publish( wrapper.Node );
		else
			parent.Next.Target = wrapper.Node;
		MarkReplacement();
		return true;
	}

	private void MarkReplacement()
	{
		ReplacementPending = true;
		ReplacementAdvanced = false;
	}

	/// <summary>Cleans completed base wrappers, advances clocks and retains completion for the next graph update.</summary>
	internal void AdvanceFrame( float delta, float phase )
	{
		CleanupCompleted();
		AdvancePreparedFrame( delta, phase, Root );
	}

	/// <summary>Advances the effective root after channel and base cleanup, retaining one completion result.</summary>
	internal void AdvancePreparedFrame( float delta, float phase, IAnimationPlaybackNode effectiveRoot )
	{
		var completion = AnimationPlaybackCompletion.Empty;
		if ( effectiveRoot is not null )
		{
			effectiveRoot.Advance( delta, phase, ref completion );
			completion = completion with { Flags = 1 };
		}
		Completion = completion;
		if ( ReplacementPending )
		{
			if ( ReplacementAdvanced )
				ReplacementPending = false;
			else
				ReplacementAdvanced = true;
		}
	}

	/// <summary>Removes eligible wrappers in insertion order, restarting the scan after every removal.</summary>
	internal int CleanupCompleted()
	{
		var removed = 0;
		for ( var i = 0; i < _retainedCount; )
		{
			var wrapper = _retained[ i ];
			if ( !wrapper.CanReleasePrevious )
			{
				i++;
				continue;
			}
			var before = _retainedCount;
			RemoveWrapper( wrapper );
			removed += before - _retainedCount;
			i = 0;
		}
		return removed;
	}

	private void RemoveWrapper( Wrapper removed )
	{
		var parentFound = false;
		for ( var i = 0; i < _retainedCount; i++ )
		{
			var parent = _retained[ i ];
			if ( ReferenceEquals( parent.Previous.Target, removed.Node ) )
			{
				parent.Previous.Target = removed.Next.Target;
				parentFound = true;
				break;
			}
			if ( ReferenceEquals( parent.Next.Target, removed.Node ) )
			{
				parent.Next.Target = removed.Next.Target;
				parentFound = true;
				break;
			}
		}
		if ( !parentFound )
			Publish( removed.Next.Target );
		ReturnTree( removed.Previous.Target );
		ReturnWrapper( removed );
	}

	private void ReturnTree( IAnimationPosePlaybackNode node )
	{
		for ( var i = 0; i < _retainedCount; i++ )
		{
			var wrapper = _retained[ i ];
			if ( !ReferenceEquals( wrapper.Node, node ) )
				continue;
			ReturnTree( wrapper.Previous.Target );
			ReturnTree( wrapper.Next.Target );
			ReturnWrapper( wrapper );
			return;
		}
		for ( var i = 0; i < _trees.Length; i++ )
		{
			if ( _trees[ i ] is not null && ReferenceEquals( _trees[ i ].Root, node ) )
			{
				_freeTrees[ _freeTreeCount++ ] = i;
				return;
			}
		}
		throw new InvalidOperationException( "The released node does not belong to a retained tree slot." );
	}

	private void ReturnWrapper( Wrapper wrapper )
	{
		if ( wrapper.IsTransition )
		{
			_freeTransitions[ _freeTransitionCount++ ] = wrapper.Index;
			Remove( _retainedTransitions, ref _transitionCount, wrapper );
		}
		else
			_freeSequences[ _freeSequenceCount++ ] = wrapper.Index;
		Remove( _retained, ref _retainedCount, wrapper );
	}

	private static void Remove( Wrapper[] entries, ref int count, Wrapper removed )
	{
		for ( var i = 0; i < count; i++ )
		{
			if ( !ReferenceEquals( entries[ i ], removed ) )
				continue;
			for ( var j = i + 1; j < count; j++ )
				entries[ j - 1 ] = entries[ j ];
			entries[ --count ] = null;
			return;
		}
	}

	private void ResetPools()
	{
		_retainedCount = 0;
		_transitionCount = 0;
		_freeTreeCount = 4;
		_freeSequenceCount = 3;
		_freeTransitionCount = 3;
		for ( var i = 0; i < 4; i++ )
			_freeTrees[ i ] = i;
		for ( var i = 0; i < 3; i++ )
		{
			_freeSequences[ i ] = i;
			_freeTransitions[ i ] = i;
			_retained[ i ] = null;
			_retainedTransitions[ i ] = null;
		}
	}

	private void Publish( IAnimationPosePlaybackNode node )
	{
		Root = node;
		_publish?.Invoke( node );
		_factory.Publish( node );
	}

	/// <summary>Prepares the published root without implicitly changing tree ownership.</summary>
	public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => Root.Prepare( enabled, consumeHistory, flags );
	/// <summary>Samples the published body and auxiliary tree.</summary>
	public void SamplePoseStreams( AnimationPoseStreamContext context ) => Root.SamplePoseStreams( context );
	/// <summary>Advances the retained clocks separately from completed-wrapper cleanup.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) => Root.Advance( delta, phase, ref completion );
	/// <summary>Seeks through the published tree's successor rules.</summary>
	public void Seek( float time ) => Root.Seek( time );
	/// <summary>Emits evaluated records through the published root, if one exists.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		if ( Root is not null )
			AnimationEvaluatedAttributeContext.Require( Root ).SampleEvaluatedAttributes( context, output, flags );
	}

	/// <summary>Queries authored attributes through the published tree's original child-selection rules.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		((IAnimationAttributePlaybackNode)Root).TryGetAuthoredAttribute( name, flags, ref result );

	private sealed class Wrapper
	{
		internal readonly int Index;
		internal readonly bool IsTransition;
		internal readonly ChildSlot Previous = new();
		internal readonly ChildSlot Next = new();
		internal IAnimationPosePlaybackNode Node;
		private AnimationSequencePosePlayback _sequence;
		private AnimationTransitionPosePlayback _transition;

		internal Wrapper( int index, bool transition )
		{
			Index = index;
			IsTransition = transition;
		}

		internal bool CanReleasePrevious => IsTransition ? _transition.Playback.CanReleasePrevious : _sequence.Playback.ActiveNext;

		internal void Reset( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next,
			float duration, AnimationTransitionMode mode, int blendMode )
		{
			Previous.Target = previous;
			Next.Target = next;
			if ( Node is null )
			{
				if ( IsTransition )
					Node = _transition = new AnimationTransitionPosePlayback( Previous, Next, duration, mode, 0 );
				else
					Node = _sequence = new AnimationSequencePosePlayback( Previous, Next );
			}
			if ( IsTransition )
				_transition.Reset( Previous, Next, duration, mode, blendMode );
			else
				_sequence.Reset( Previous, Next );
		}
	}

	private sealed class ChildSlot : IAnimationPosePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
	{
		internal IAnimationPosePlaybackNode Target;
		public SkeletonPose Pose => Target.Pose;
		public float Duration => Target.Duration;
		public float Elapsed => Target.Elapsed;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => Target.Prepare( enabled, consumeHistory, flags );
		public void SamplePoseStreams( AnimationPoseStreamContext context ) => Target.SamplePoseStreams( context );
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) => Target.Advance( delta, phase, ref completion );
		public void Seek( float time ) => Target.Seek( time );
		public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
			AnimationEvaluatedAttributeBuffer output, byte flags ) =>
			AnimationEvaluatedAttributeContext.Require( Target ).SampleEvaluatedAttributes( context, output, flags );

		public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
			((IAnimationAttributePlaybackNode)Target).TryGetAuthoredAttribute( name, flags, ref result );
	}
}
