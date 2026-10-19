namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied response resources for non-launch board forces.</summary>
internal readonly record struct PhysicalBoardForceTuning( float ResponseGain, float MaximumVelocityChange,
	float ApplicationHeight, float VelocityGain, float TargetSpeed );
