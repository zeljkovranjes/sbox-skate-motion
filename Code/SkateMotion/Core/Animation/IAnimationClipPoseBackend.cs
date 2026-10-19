namespace SkateMotion.Core.Animation;

/// <summary>Clip lookup and pose sampling supplied by an animation backend.</summary>
internal interface IAnimationClipPoseBackend
{
	bool Suppressed { get; }
	ushort ResolveClip();
	void Sample( ushort clip, float start, float current, uint wraps, uint flags );
}
