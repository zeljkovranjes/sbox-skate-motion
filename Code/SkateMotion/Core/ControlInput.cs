namespace SkateMotion.Core;

internal readonly record struct ControlInput(
	float Steer = 0f, float Push = 0f, float Brake = 0f,
	float FlickX = 0f, float FlickY = 0f, float Lean = 0f,
	bool Slide = false, bool Grab = false, bool Recover = false, bool Goofy = false, bool Pump = false,
	bool? PushContact = null )
{
	/// <summary>The hand plant button: off a steep ramp's lip, it plants a hand on the lip (see <see cref="Vehicle.HandPlant"/>).</summary>
	internal bool HandPlant { get; init; }

	/// <summary>The rider's animation holds the board dark side up: on the ground it skids on its grip, nothing pushes
	/// it, and a bail lets it go upside down.</summary>
	internal bool DarkSideUp { get; init; }

	internal ControlInput Sanitized() => new(
		Axis( Steer ), Math.Clamp( Axis( Push ), 0f, 1f ), Math.Clamp( Axis( Brake ), 0f, 1f ),
		Axis( FlickX ), Axis( FlickY ), Axis( Lean ), Slide, Grab, Recover, Goofy, Pump, PushContact )
	{
		HandPlant = HandPlant,
		DarkSideUp = DarkSideUp
	};

	private static float Axis( float value ) => float.IsFinite( value ) ? Math.Clamp( value, -1f, 1f ) : 0f;
}
