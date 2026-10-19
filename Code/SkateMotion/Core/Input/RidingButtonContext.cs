namespace SkateMotion.Core.Input;

/// <summary>Movement-state restrictions and stance used when resolving controller requests.</summary>
internal readonly record struct RidingButtonContext(
	bool SuppressPush, bool SuppressSimultaneousPush, bool SuppressBrake,
	bool SuppressDarkCatch, bool SuppressNewToggle, bool MountPrepared, bool AutoPush, bool Goofy );
