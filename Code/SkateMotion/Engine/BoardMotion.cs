using SkateMotion.Core.Vehicle;

namespace SkateMotion.Engine;

/// <summary>
/// What the shown board follows this frame besides its pose: the steering and riding that lean its deck into a turn,
/// and which wheels are on the ground (bit per wheel, physics order).
/// </summary>
internal readonly record struct BoardMotion( float Steer, float Speed, bool Pushing, DeckLeanMode Lean, int TouchingWheels )
{
	internal bool Touching( int wheel ) => (TouchingWheels & (1 << wheel)) != 0;
}
