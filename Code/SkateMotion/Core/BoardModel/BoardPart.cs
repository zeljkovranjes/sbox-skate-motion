namespace SkateMotion.Core.BoardModel;

/// <summary>
/// A part of a skateboard model. Wheels follow the physics order (front left, front right, rear left, rear right) and
/// trucks front then rear; a baseplate is the part of a truck fixed to the deck, the truck its hanger.
/// </summary>
internal enum BoardPart
{
	Deck,
	FrontBaseplate,
	RearBaseplate,
	FrontTruck,
	RearTruck,
	FrontLeftWheel,
	FrontRightWheel,
	RearLeftWheel,
	RearRightWheel
}
