namespace SkateMotion.Core.Animation;

/// <summary>Publishes retained descriptors and resolved metadata to movement classification.</summary>
internal readonly record struct AnimationContactHistoryOutput(
	AnimationContactDescriptor Candidate, AnimationContactDescriptor Current,
	AnimationContactDescriptor Confirmed, int FirstIndex, int SecondIndex,
	AnimationMovementContactData Contact );
