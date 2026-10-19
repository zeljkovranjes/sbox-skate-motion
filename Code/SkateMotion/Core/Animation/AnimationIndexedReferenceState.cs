namespace SkateMotion.Core.Animation;

/// <summary>Retains a one-use indexed reference for the next successfully constructed base motion.</summary>
internal sealed class AnimationIndexedReferenceState
{
	private readonly Func<int, IAnimationPosePlaybackNode> _reference;

	internal AnimationIndexedReferenceState( Func<int, IAnimationPosePlaybackNode> reference ) =>
		_reference = reference ?? throw new ArgumentNullException( nameof( reference ) );

	internal int BaseIndex { get; set; } = -1;
	internal int MaximumIndex { get; set; } = -1;
	internal int Offset { get; set; } = -1;
	internal bool Pending { get; set; }

	internal IAnimationPosePlaybackNode Compose( IAnimationPosePlaybackNode motion )
	{
		if ( motion is null || !Pending || Offset < 0 || BaseIndex < 0 )
			return motion;
		var index = Math.Min( unchecked( Offset + BaseIndex ), MaximumIndex );
		var root = new AnimationCompositionPosePlayback( motion, _reference( index ), true );
		Pending = false;
		return root;
	}
}
