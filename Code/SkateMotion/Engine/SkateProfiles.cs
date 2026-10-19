using SkateMotion.Core;
using SkateMotion.Resources;

namespace SkateMotion.Engine;

internal static class SkateProfiles
{
	internal static SkateSettings Read( SkateProfile profile )
	{
		if ( profile is null )
			return SkateSettings.RecoveredDefaults;

		if ( !float.IsFinite( profile.WheelRadius ) || profile.WheelRadius <= 0f
			|| profile.WheelRadius >= profile.RideHeight )
			throw new ArgumentException( "Wheel radius must be positive and less than ride height." );

		var settings = (profile.Preset == SkatePreset.Balanced ? SkateSettings.RecoveredDefaults : new SkateSettings()) with
		{
			Mass = profile.Mass,
			Gravity = profile.Gravity,
			Wheelbase = profile.Wheelbase,
			Track = profile.Track,
			RideHeight = profile.RideHeight,
			Grip = profile.Grip,
			RollingResistance = profile.RollingResistance,
			PushAcceleration = profile.PushAcceleration,
			PushSpeed = profile.PushSpeed,
			PopSpeed = profile.PopSpeed,
			BailSpeed = profile.BailSpeed,
			Difficulty = (Core.HandlingDifficulty)profile.Difficulty,
			AutoPump = profile.AutoPump
		};
		settings = profile.Preset switch
		{
			SkatePreset.Simulation => settings with
			{
				BalanceSpring = 90f, BalanceDamping = 18f, PopSpeed = settings.PopSpeed * 0.8f
			},
			SkatePreset.Arcade => settings with
			{
				Grip = settings.Grip * 1.2f, PushAcceleration = settings.PushAcceleration * 1.25f,
				PopSpeed = settings.PopSpeed * 1.2f, BalanceSpring = 250f, BalanceDamping = 40f
			},
			_ => settings
		};
		settings.Validate();
		return settings;
	}
}
