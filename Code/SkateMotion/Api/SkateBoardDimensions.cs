namespace SkateMotion;

/// <summary>
/// A skateboard's size as it is simulated and as the rider's animation is fitted to it, in metres: measured from its
/// board model when one is set (see <see cref="Components.Skateboard.BoardModel"/>), otherwise the generated board's or
/// the custom deck's, with the handling profile's wheelbase, track, wheel radius and ride height.
/// </summary>
/// <param name="Length">The deck's length, nose to tail.</param>
/// <param name="Width">The deck's width at its middle.</param>
/// <param name="Thickness">The deck's thickness at its middle.</param>
/// <param name="Wheelbase">Distance between the front and rear axles.</param>
/// <param name="Track">Distance between the left and right wheels' centres.</param>
/// <param name="WheelRadius">Radius of the wheels.</param>
/// <param name="RideHeight">Height of the middle of the deck above the ground.</param>
/// <param name="Source">Where the shown parts come from.</param>
public readonly record struct SkateBoardDimensions( float Length, float Width, float Thickness, float Wheelbase, float Track,
	float WheelRadius, float RideHeight, SkateBoardSource Source );
