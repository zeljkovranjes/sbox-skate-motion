namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Reusable trajectory storage and retained positional and angular approach state.</summary>
internal sealed class GrindApproachState
{
	internal const int SampleCapacity = 12;
	internal const int PointCount = 5;
	internal LimbRigidTransform[] Poses { get; } = new LimbRigidTransform[ SampleCapacity ];
	internal Vector3[] Points { get; } = new Vector3[ SampleCapacity * PointCount ];
	internal float[] Angles { get; } = new float[ SampleCapacity ];
	internal float[] Crossings { get; } = new float[ PointCount ];
	internal float[] Distances { get; } = new float[ PointCount ];
	internal Vector3[] Corrections { get; } = new Vector3[ PointCount ];
	internal LimbFrame EdgeFrame { get; set; }
	internal LimbFrame VelocityFrame { get; set; }
	internal bool HasCandidate { get; set; }
	internal bool RequestValid { get; set; }
	internal int SelectedCandidate { get; set; } = -1;
	internal int PreviousCandidate { get; set; } = -1;
	internal int SelectedPoint { get; set; } = -1;
	internal float SelectedAngle { get; set; }
	internal float EarliestCrossing { get; set; }
	internal Vector3 CorrectionVelocity { get; set; }
	internal Vector3 CorrectionPosition { get; set; }
	internal Vector3 AngularVelocity { get; set; }
	internal Vector3 AngularPosition { get; set; }
	internal float PitchAngle { get; set; }
	internal float YawError { get; set; }
}
