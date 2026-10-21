using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>Turns a grab picked from the hands and the trick stick into the board input's grab.</summary>
internal static class AirGrabs
{
	internal static SkateAirGrab From( GrabPick pick ) => pick switch
	{
		GrabPick.Indy => SkateAirGrab.Indy,
		GrabPick.Melon => SkateAirGrab.Melon,
		GrabPick.Mute => SkateAirGrab.Mute,
		GrabPick.Stale => SkateAirGrab.Stale,
		GrabPick.Nose => SkateAirGrab.Nose,
		GrabPick.NoseCrail => SkateAirGrab.NoseCrail,
		GrabPick.Tail => SkateAirGrab.Tail,
		GrabPick.Seatbelt => SkateAirGrab.Seatbelt,
		GrabPick.Double => SkateAirGrab.Crail,
		GrabPick.Rocket => SkateAirGrab.RocketAir,
		_ => SkateAirGrab.None
	};
}
