namespace SkateMotion.Core.Physics;

/// <summary>Physical observations and evaluated animation flags for one board ownership decision.</summary>
internal readonly record struct PhysicalBoardInput(
	int State, ulong ActionFlags, ulong RigFlags, ulong TimingFlags,
	float RetrievalProgress, bool GroundCheck, int BodyMode,
	bool BoardContact, bool SelectedJointContact )
{
	internal int ContactJointIndex => ( ActionFlags & 4UL ) != 0 ? 3 : 7;
	internal bool Holding => State == 1;
	internal bool Retrieving => State == 4;
}
