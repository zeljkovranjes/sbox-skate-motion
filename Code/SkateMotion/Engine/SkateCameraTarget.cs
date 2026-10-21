using Sandbox;

namespace SkateMotion.Engine;

/// <summary>What the skate camera follows this frame, read from the rider and board components (engine units).</summary>
/// <param name="Mode">The rider's mode (riding when there is no rider).</param>
/// <param name="Board">The board as shown this frame (the target's transform without a board).</param>
/// <param name="Rider">The rider as shown this frame (the board's without a rider).</param>
/// <param name="Velocity">The board's velocity while riding, the walker's on foot (inches per second).</param>
/// <param name="State">The board's state.</param>
/// <param name="RiderAirborne">The walking rider is off the ground.</param>
/// <param name="RiderHeight">The rider's standing height in inches, 0 without a rider.</param>
/// <param name="Ignored">Objects camera traces pass through: the target, board, rider and the rider's model.</param>
internal readonly record struct SkateCameraTarget( SkateRiderMode Mode, Transform Board, Transform Rider, Vector3 Velocity,
	SkateState State, bool RiderAirborne, float RiderHeight, GameObject[] Ignored );
