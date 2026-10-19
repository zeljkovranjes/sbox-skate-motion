namespace SkateMotion.Core.Animation;

/// <summary>Retained physical locomotion phase, including its current settling request.</summary>
internal readonly record struct PhysicalLocomotionCadenceState(
	float Phase, float Rate, float Target, float? Duration, bool ForwardTarget, int LocomotionIndex )
{
	/// <summary>Creates the initial physical phase; no target duration has been supplied.</summary>
	internal static PhysicalLocomotionCadenceState Initial => new( 0f, 0f, -1f, null, false, 0 );
}
