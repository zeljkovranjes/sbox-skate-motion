namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained query origin and result fields, whose validity is cleared before submission.</summary>
internal sealed class PhysicalBoardProbeCache
{
	internal Vector3 Origin { get; set; }
	internal Vector3 Position { get; set; }
	internal Vector3 Normal { get; set; }
	internal int Material { get; set; }
	internal bool Valid { get; set; }

	internal void Clear()
	{
		Origin = Position = Normal = Vector3.Zero;
		Material = 0;
		Valid = false;
	}
}
