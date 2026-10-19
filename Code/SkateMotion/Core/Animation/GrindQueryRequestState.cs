namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector4 = System.Numerics.Vector4;

/// <summary>Reusable direction and contact slots with retained request-builder outputs.</summary>
internal sealed class GrindQueryRequestState
{
	internal Vector4[] Directions { get; } = new Vector4[ 7 ];
	internal TrajectoryQueryTarget[] Targets { get; } = [new(), new(), new(), new(), new(), new(), new()];
	internal Vector4 Reference { get; set; }
	internal Vector4 Origin { get; set; }
	internal Vector4 LocalReference { get; set; }
	internal Vector4 BoardOffset { get; set; }
	internal Vector4 LocalBoardOffset { get; set; }
	internal Vector4 RetainedOffset { get; set; }
	internal Vector4 RetainedAxis { get; set; }
	internal float Height { get; set; }
	internal float Duration { get; set; }
	internal int Count { get; set; }
	internal bool Special { get; set; }
	internal bool Ready { get; set; }
	internal byte SelectionFlag { get; set; }
	internal byte SecondarySelectionFlag { get; set; }
}
