namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Retained selection outputs and reusable candidate/query slots.</summary>
internal sealed class GrindCandidateSelectionState
{
	internal GrindScoringCandidate[] Candidates { get; } = new GrindScoringCandidate[ 7 ];
	internal GrindQueryRequestState Queries { get; } = new();
	internal GrindCandidateQueryTransfer Transfer { get; } = new();
	internal GrindCandidateProductionState Production;
	internal bool Capture;
	internal ushort UpdateCount { get; set; }
	internal short SelectedIndex { get; set; }
	internal bool HasSelection { get; set; }
	internal bool Active { get; set; }
	internal bool Repeated { get; set; }
	internal bool Early { get; set; }
	internal int SelectedKind { get; set; }
	internal GrindScoringCandidate Selected { get; set; }
	internal GrindScoringFrame SelectedFrame { get; set; }
	internal Vector4 SelectedVelocity { get; set; }
	internal Vector4 FallbackDirection { get; set; }
	internal GrindTrajectoryState Trajectory { get; set; }
	internal GrindQueryRequestInput Request { get; set; }
	internal bool HasInput { get; set; }
	internal bool MotionSpecial { get; set; }
}
