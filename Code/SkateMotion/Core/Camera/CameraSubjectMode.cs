namespace SkateMotion.Core.Camera;

/// <summary>What the camera is following.</summary>
internal enum CameraSubjectMode
{
	/// <summary>A rider on the board (or a board on its own).</summary>
	Riding,

	/// <summary>A rider walking or carrying the board.</summary>
	OnFoot,

	/// <summary>A bailed rider's ragdoll.</summary>
	Bailed
}
