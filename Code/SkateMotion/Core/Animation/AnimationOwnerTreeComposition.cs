namespace SkateMotion.Core.Animation;

/// <summary>Applies owner reference and posture wrappers in retained construction order.</summary>
internal sealed class AnimationOwnerTreeComposition
{
	private readonly Func<IAnimationPosePlaybackNode> _reference;
	private readonly Func<IAnimationPosePlaybackNode> _beforeReference;
	private readonly Func<IAnimationPosePlaybackNode> _afterReference;
	private readonly Func<IAnimationPostureTreeProvider> _posture;
	private readonly Func<ulong> _readFlags;
	private readonly Action<ulong> _writeFlags;

	internal AnimationOwnerTreeComposition( Func<IAnimationPosePlaybackNode> reference,
		Func<IAnimationPosePlaybackNode> beforeReference, Func<IAnimationPosePlaybackNode> afterReference,
		Func<IAnimationPostureTreeProvider> posture,
		Func<ulong> readFlags, Action<ulong> writeFlags )
	{
		_reference = reference ?? throw new ArgumentNullException( nameof( reference ) );
		_beforeReference = beforeReference ?? throw new ArgumentNullException( nameof( beforeReference ) );
		_afterReference = afterReference ?? throw new ArgumentNullException( nameof( afterReference ) );
		_posture = posture ?? throw new ArgumentNullException( nameof( posture ) );
		_readFlags = readFlags ?? throw new ArgumentNullException( nameof( readFlags ) );
		_writeFlags = writeFlags ?? throw new ArgumentNullException( nameof( writeFlags ) );
	}

	/// <summary>Preserves the motion clock while applying each requested wrapper, including duplicate posture wrappers.</summary>
	internal IAnimationPosePlaybackNode Compose( IAnimationPosePlaybackNode motion, bool usePosture )
	{
		if ( motion is null ) return null;
		IAnimationPosePlaybackNode root = new AnimationCompositionPosePlayback( motion, _reference(), true );
		var flags = _readFlags();
		if ( (flags & 0x80000000UL) != 0 )
		{
			root = new AnimationCompositionPosePlayback( _beforeReference(), root, false );
			root = new AnimationCompositionPosePlayback( root, _afterReference(), true );
			flags = _readFlags();
		}
		if ( (flags & 0x40000000UL) != 0 )
		{
			root = _posture().Wrap( root, 2 );
			flags = _readFlags();
		}
		if ( (flags & 0x00400000UL) != 0 )
		{
			var provider = _posture();
			flags = _readFlags();
			root = provider.Wrap( root, (flags & 0x00200000UL) != 0 ? 1 : 2 );
			flags = _readFlags() & ~0x00400000UL;
			_writeFlags( flags );
		}
		_writeFlags( flags & ~0x00200000UL );
		return usePosture ? _posture().Wrap( root, 2 ) : root;
	}
}
