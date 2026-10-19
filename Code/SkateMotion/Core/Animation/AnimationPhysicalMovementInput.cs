namespace SkateMotion.Core.Animation;

/// <summary>Physical snapshot fields used by retained movement classification.</summary>
internal readonly record struct AnimationPhysicalMovementInput(
	AnimationPhysicalControllerCodes Controllers,
	int BoardContactType, bool BoardHasContacts, bool PrimarySecondaryMode,
	bool ContactTransition, bool OffboardRideMode, bool OffboardSecondaryMode,
	AnimationMovementContactData Contact );
