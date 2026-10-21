namespace SkateMotion.Core.Animation;

/// <summary>
/// One hand's finger bones and poses: the clip skeleton's hand bone they hang from, the clip stream that picks their
/// pose, the bank (bones, reference pose and a clip whose frames are the poses) and that clip's name.
/// </summary>
internal sealed record HandPoseBank( string Hand, AnimationPoseStreamKey Stream, RawAnimationClipSet Bones, string Clip );
