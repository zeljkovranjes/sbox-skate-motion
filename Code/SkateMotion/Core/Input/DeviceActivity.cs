namespace SkateMotion.Core.Input;

/// <summary>Which parts of a controller are in use on purpose this frame, beyond drift and resting triggers.</summary>
/// <param name="LeftStick">The left stick is pushed.</param>
/// <param name="RightStick">The right stick is pushed.</param>
/// <param name="Triggers">A trigger is pulled.</param>
internal readonly record struct DeviceActivity( bool LeftStick, bool RightStick, bool Triggers );
