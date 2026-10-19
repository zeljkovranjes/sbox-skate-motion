namespace SkateMotion.Core.Animation;

/// <summary>Refreshes enabled hand candidates and binds their retained output to hand contacts.</summary>
internal sealed class PhysicalHandInteractionController
{
	private readonly PhysicalHandCandidateController _candidate;
	private readonly PhysicalHandContactController _contact;

	internal PhysicalHandInteractionController( PhysicalHandCandidateController candidate,
		PhysicalHandContactController contact )
	{
		ArgumentNullException.ThrowIfNull( candidate );
		ArgumentNullException.ThrowIfNull( contact );
		_candidate = candidate;
		_contact = contact;
	}

	internal void Update( PhysicalHandCandidateState candidate, PhysicalHandContactState contact,
		EffectorRigState rig, EffectorPoseCache cache, bool[] pinning,
		GrindSurfaceQueryResult surface, PhysicalHandInteractionInput input )
	{
		if ( (input.ActionFlags & (1UL << 22)) == 0 )
			return;
		_candidate.Update( candidate, input.Candidate, surface );
		if ( (candidate.Flags & (1UL << 31)) == 0 )
			return;

		var requestFlags = ((input.ActionFlags >> 2) & 1UL) << 63;
		var reset = (input.RigFlags & (1UL << 61)) != 0 || (input.RigFlags & (1UL << 60)) == 0;
		if ( reset )
			requestFlags |= 1UL << 62;
		_contact.Update( contact, rig, cache, pinning, new PhysicalHandContactInput(
			input.ModelToWorld, candidate.Point, candidate.Metric, candidate.Flags, requestFlags ) );
	}
}
