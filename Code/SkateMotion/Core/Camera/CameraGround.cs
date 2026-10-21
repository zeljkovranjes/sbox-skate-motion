namespace SkateMotion.Core.Camera;

/// <summary>Ground found around the subject this frame, in metres.</summary>
/// <param name="Distances">Horizontal distance of each probe ahead along the travel.</param>
/// <param name="Grounds">Ground height under each probe, NaN where the way to it was blocked.</param>
/// <param name="Count">Probes in use (0 when not moving).</param>
/// <param name="Below">Ground height under the subject, NaN when there is none in reach.</param>
internal readonly record struct CameraGround( float[] Distances, float[] Grounds, int Count, float Below )
{
	/// <summary>No ground known.</summary>
	internal static CameraGround None { get; } = new( [], [], 0, float.NaN );
}
