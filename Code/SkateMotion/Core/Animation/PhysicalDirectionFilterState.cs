namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Retained vector response and its four authored filter coefficients.</summary>
internal sealed class PhysicalDirectionFilterState
{
	internal Vector4 Coefficients { get; set; }
	internal Vector3 Value { get; set; }
	internal Vector3 PreviousInput { get; set; }
	internal Vector3 SmoothedError { get; set; }
	internal Vector3 PreviousDelta { get; set; }
	internal Vector3 SmoothedDelta { get; set; }
}
