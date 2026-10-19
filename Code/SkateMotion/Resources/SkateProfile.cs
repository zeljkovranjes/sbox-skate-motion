using Sandbox;

namespace SkateMotion.Resources;

/// <summary>Estimated physical parameters in SI units; choose a preset and tune for your game.</summary>
[AssetType( Name = "Skate Profile", Extension = "skate" )]
public sealed class SkateProfile : GameResource
{
	/// <summary>Response preset applied to the base parameters.</summary>
	[Property] public SkatePreset Preset { get; set; } = SkatePreset.Balanced;

	/// <summary>How forgiving the measured handling (the Balanced preset) is.</summary>
	[Property] public SkateDifficulty Difficulty { get; set; } = SkateDifficulty.Normal;

	/// <summary>Lets the rider pump a little without the pump button, rising as steep ground flattens out (dropping into a
	/// transition gains speed). Off, a board coasting with no input never gains speed.</summary>
	[Property] public bool AutoPump { get; set; }

	/// <summary>Effective combined board and rider mass in kilograms.</summary>
	[Property, Range( 20f, 160f )] public float Mass { get; set; } = 80f;

	/// <summary>Downward acceleration in metres per second squared.</summary>
	[Property, Range( 0.1f, 30f )] public float Gravity { get; set; } = 9.81f;

	/// <summary>Distance between front and rear wheel axles in metres.</summary>
	[Property, Range( 0.2f, 1f )] public float Wheelbase { get; set; } = 0.52f;

	/// <summary>Distance between left and right wheel centres in metres.</summary>
	[Property, Range( 0.1f, 0.4f )] public float Track { get; set; } = 0.19f;

	/// <summary>Wheel radius in metres.</summary>
	[Property, Range( 0.015f, 0.05f )] public float WheelRadius { get; set; } = 0.0285f;

	/// <summary>Unloaded deck-origin height above the supporting surface in metres.</summary>
	[Property, Range( 0.06f, 0.2f )] public float RideHeight { get; set; } = 0.095f;

	/// <summary>Base friction coefficient, multiplied by the contacted map surface.</summary>
	[Property, Range( 0.05f, 2f )] public float Grip { get; set; } = 0.85f;

	/// <summary>Rolling resistance as a fraction of contact load.</summary>
	[Property, Range( 0f, 0.1f )] public float RollingResistance { get; set; } = 0.012f;

	/// <summary>Mean push acceleration before speed falloff.</summary>
	[Property, Range( 0f, 15f )] public float PushAcceleration { get; set; } = 5f;

	/// <summary>Speed at which pushing stops adding energy; downhill motion is not capped.</summary>
	[Property, Range( 1f, 20f )] public float PushSpeed { get; set; } = 8.5f;

	/// <summary>Vertical velocity change for a fully charged pop.</summary>
	[Property, Range( 0.5f, 8f )] public float PopSpeed { get; set; } = 4.1f;

	/// <summary>Estimated normal collision speed above which the rider bails, in metres per second.</summary>
	[Property, Range( 1f, 30f )] public float BailSpeed { get; set; } = 8f;
}
