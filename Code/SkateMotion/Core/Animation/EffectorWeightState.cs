namespace SkateMotion.Core.Animation;

/// <summary>A rig effector's mode and independent target/override weights.</summary>
internal readonly record struct EffectorWeightState( int Mode, float Target, float Override );
