using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>World endpoints and retained metadata for an edge in the rig's Y-up frame.</summary>
internal readonly record struct GrindEdgeRecord( RailSegment Segment, uint Metadata, uint Flags )
{
	/// <summary>Includes zero-length edges and edges with negligible horizontal span.</summary>
	internal bool IsVertical => (Flags & 0x80000000u) != 0;

	internal static GrindEdgeRecord FromCoefficients( Vector3 first, Vector3 second, Vector3 third,
		Vector3 fourth, LimbRigidTransform frame, uint metadata, uint flags )
	{
		var start = frame.Rotation.Transform( fourth, frame.Translation );
		// Grouped endpoint evaluation preserves cancellation and rounding.
		var endpoint = (first + second) + (third + fourth);
		var end = frame.Rotation.Transform( endpoint, frame.Translation );
		var difference = end - start;
		var vertical = !(MathF.Abs( difference.X ) > 0.0000152587890625f
			|| MathF.Abs( difference.Z ) > 0.0000152587890625f);
		flags = (flags & 0x7fffffffu) | (vertical ? 0x80000000u : 0u);
		return new GrindEdgeRecord( new RailSegment( start, end ), metadata, flags );
	}
}
