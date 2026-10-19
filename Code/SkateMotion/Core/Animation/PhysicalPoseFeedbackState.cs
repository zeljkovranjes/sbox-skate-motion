namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retains weighted movement and the selected postsolve animation correction.</summary>
internal sealed class PhysicalPoseFeedbackState
{
	internal Vector3 Position { get; set; }
	internal Vector3 PreviousPosition { get; set; }
	internal Vector3 Velocity { get; set; }
	internal Vector3 Correction { get; set; }
	internal Vector3 FirstFeedback { get; set; }
	internal Vector3 SecondFeedback { get; set; }
	internal float MaximumError { get; set; }
}
