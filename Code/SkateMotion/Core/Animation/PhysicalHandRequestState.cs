namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retains request inputs and source output independently of candidate evaluation.</summary>
internal sealed class PhysicalHandRequestState
{
	internal PhysicalHandSourceState Source { get; } = new();
	internal PhysicalHandRequestInput Input { get; set; }
	internal Vector3 PreviousPoint { get; set; }

	internal PhysicalHandCandidateInput CandidateInput => new(
		Source.Enabled, Source.Point, Source.Edge, Input.Origin, Input.Velocity,
		Input.Normal, Input.Side, Input.Tangent, PreviousPoint );
}
