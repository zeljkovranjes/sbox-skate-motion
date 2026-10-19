namespace SkateMotion.Core.Input;

[Flags]
internal enum ControllerButtons : uint
{
	None = 0,
	LeftStick = 1u << 31,
	RightStick = 1u << 30,
	LeftBumper = 1u << 29,
	RightBumper = 1u << 28,
	Up = 1u << 27,
	Down = 1u << 26,
	Left = 1u << 25,
	Right = 1u << 24,
	West = 1u << 23,
	North = 1u << 22,
	South = 1u << 21,
	East = 1u << 20
}
