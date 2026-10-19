namespace SkateMotion.Core.Animation;

/// <summary>Velocity and position drive coefficients for one joint constraint.</summary>
internal readonly record struct PhysicalJointDrive( float VelocityGain, float MaximumForce, float PositionGain, int Mode );
