namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Owner fields consumed when preparing the board's wheel, ground and forward queries.</summary>
internal readonly record struct PhysicalBoardProbeInput( int PreviousMode, Vector3 Heading,
	Vector3 SurfaceNormal, float ContactStrength, Vector3 BoardPosition, Vector3 WheelDirection );
