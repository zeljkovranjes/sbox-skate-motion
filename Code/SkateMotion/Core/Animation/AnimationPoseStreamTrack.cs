namespace SkateMotion.Core.Animation;

/// <summary>Immutable named pose selections with an explicit paired stream identity.</summary>
internal sealed class AnimationPoseStreamTrack
{
	private readonly AnimationPoseStreamKeyframe[] _keys;

	internal AnimationPoseStreamTrack( AnimationPoseStreamKey name, AnimationPoseStreamKey pairedName,
		AnimationPoseStreamKeyframe[] keys )
	{
		ArgumentNullException.ThrowIfNull( keys );
		for ( var index = 0; index < keys.Length; index++ )
		{
			if ( keys[ index ].Value > 127 || ( index > 0 && keys[ index ].Time < keys[ index - 1 ].Time ) )
				throw new ArgumentException( "Pose keys need nondecreasing signed times and seven-bit selections.", nameof( keys ) );
		}
		Name = name;
		PairedName = pairedName;
		_keys = new AnimationPoseStreamKeyframe[ keys.Length ];
		Array.Copy( keys, _keys, keys.Length );
	}

	internal AnimationPoseStreamKey Name { get; }
	internal AnimationPoseStreamKey PairedName { get; }
	internal ReadOnlySpan<AnimationPoseStreamKeyframe> Keys => _keys;
}
