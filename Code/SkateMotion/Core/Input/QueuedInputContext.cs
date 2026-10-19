namespace SkateMotion.Core.Input;

internal readonly record struct QueuedInputContext(
	bool SuppressFingerCommands,
	bool SuppressLateCommands,
	bool BlockAllDuringLateRestriction,
	bool SuppressFlips,
	bool SuppressSlides,
	bool RestrictToBasicTricks,
	bool HasAirGrab,
	bool Pushing );
