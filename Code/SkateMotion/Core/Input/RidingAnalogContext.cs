namespace SkateMotion.Core.Input;

/// <summary>Independent movement restrictions for analog action groups.</summary>
internal readonly record struct RidingAnalogContext(
	bool SuppressSpin, bool SuppressTurn, bool SuppressSlide, bool AutoPushActive );
