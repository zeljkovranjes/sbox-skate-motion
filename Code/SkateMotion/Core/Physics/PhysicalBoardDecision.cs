namespace SkateMotion.Core.Physics;

/// <summary>Ordered physical work requested by a board ownership update; actuators perform state entry separately.</summary>
internal readonly record struct PhysicalBoardDecision(
	int NextState, bool ChangeState = false, bool QueryGround = false,
	bool UpdateHeld = false, bool ClearVelocity = false,
	bool UpdateContactSpring = false, float ContactSpringStrength = 0f );
