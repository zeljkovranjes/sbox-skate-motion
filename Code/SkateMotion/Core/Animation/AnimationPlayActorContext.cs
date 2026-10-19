using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects playback entry to shared actor, selector and indexed-reference storage.</summary>
internal sealed class AnimationPlayActorContext : IAnimationPlayActorContext
{
	private readonly AnimationPlayActorState _state;
	private readonly IDictionary<AnimationAttributeKey, AnimationAttributeKey> _selectors;
	private readonly AnimationAttributeKey _characterSelector;
	private readonly Func<BoardHandlingInput> _physicalOutput;
	private readonly AnimationIndexedReferenceState _indexedReference;

	internal AnimationPlayActorContext( AnimationPlayActorState state,
		IDictionary<AnimationAttributeKey, AnimationAttributeKey> selectors,
		AnimationAttributeKey characterSelector, Func<BoardHandlingInput> physicalOutput,
		AnimationIndexedReferenceState indexedReference )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( selectors );
		ArgumentNullException.ThrowIfNull( physicalOutput );
		ArgumentNullException.ThrowIfNull( indexedReference );
		_state = state;
		_selectors = selectors;
		_characterSelector = characterSelector;
		_physicalOutput = physicalOutput;
		_indexedReference = indexedReference;
	}

	public void RefreshCharacterSelection() => _selectors[ _characterSelector ] = _state.CharacterSelection;
	public AnimationPlayTransition ReadTransitionOverride() => _state.TransitionOverride;
	public void ConsumeTransitionOverride() => _state.ConsumeTransitionOverride();
	public bool IsAlternateStance() => _state.AlternateStanceValue == 1;
	public bool IsMirrored() => (_state.Flags & (1UL << 30)) != 0;
	public bool UseOffBoardVariant() => !_physicalOutput().HoldingBoard;
	public void SetActorMode( byte mode ) => _indexedReference.Pending = mode != 0;
}
