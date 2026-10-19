namespace SkateMotion.Core.Animation;

/// <summary>Playback clock operations shared by clips and composite animation nodes.</summary>
internal interface IAnimationPlaybackNode
{
	float Duration { get; }
	float Elapsed { get; }
	void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion );
	void Seek( float time );
}
