namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Inclusive edge-query bounds in the rig's Y-up frame.</summary>
internal readonly record struct GrindEdgeBounds( Vector3 Minimum, Vector3 Maximum )
{
	internal bool Overlaps( GrindEdgeBounds other ) =>
		!(Minimum.X > other.Maximum.X || Minimum.Y > other.Maximum.Y || Minimum.Z > other.Maximum.Z
		|| other.Minimum.X > Maximum.X || other.Minimum.Y > Maximum.Y || other.Minimum.Z > Maximum.Z);
}
