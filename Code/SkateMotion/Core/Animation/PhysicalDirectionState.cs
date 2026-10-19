namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Physical direction frames, response filters and inactive steering history.</summary>
internal sealed class PhysicalDirectionState
{
	internal PhysicalDirectionFilterState PrimaryFilter { get; } = new();
	internal PhysicalDirectionFilterState FirstFilter { get; } = new();
	internal PhysicalDirectionFilterState SecondFilter { get; } = new();
	internal Vector3 Direction { get; set; }
	internal Vector3 PreviousDirection { get; set; }
	internal Vector3 PublishedDirection { get; set; }
	internal Vector3 DirectionDelta { get; set; }
	internal Vector3 SecondaryDirection { get; set; }
	internal Vector3 FilteredDirection { get; set; }
	internal Vector3 Tilt { get; set; }
	internal LimbRigidTransform FilteredFrame { get; set; }
	internal LimbRigidTransform WorldFrame { get; set; }
	internal LimbRigidTransform LocalFrame { get; set; }
	internal LimbRigidTransform InverseFrame { get; set; }
	internal LimbRigidTransform SourceFrame { get; set; }
	internal float FirstRate { get; set; }
	internal float SecondRate { get; set; }
	internal float Control { get; set; }
	internal float ControlDelta { get; set; }
	internal float ControlResponse { get; set; }
	internal float SmoothedControl { get; set; }
	internal float SteeringVelocity { get; set; }
	internal float SteeringTarget { get; set; }
	internal float SteeringMagnitude { get; set; }
	internal float SecondaryControl { get; set; }
	internal bool AlternativeCurve { get; set; }
	internal float[] ControlHistory { get; } = new float[ 30 ];
	internal int HistoryIndex { get; set; }
}
