namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Resets retained contact state, gates source production and prepares surface probes.</summary>
internal sealed class PhysicalHandRequestController
{
	private readonly PhysicalHandSourceEligibility _eligibility;
	private readonly PhysicalHandSourceController _source;
	private readonly GrindSurfaceProbePlan _plan;
	private readonly float _reach;
	internal GrindSurfaceProbePlan Plan => _plan;
	internal float Reach => _reach;

	internal PhysicalHandRequestController( PhysicalHandSourceEligibility eligibility,
		PhysicalHandSourceController source, GrindSurfaceProbePlan plan, float reach )
	{
		ArgumentNullException.ThrowIfNull( eligibility );
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( plan );
		_eligibility = eligibility;
		_source = source;
		_plan = plan;
		_reach = reach;
	}

	internal bool Prepare( GrindEdgeChunk[] chunks, PhysicalHandRequestState state,
		PhysicalHandCandidateState candidate, PhysicalHandRequestInput input, Vector3 reference )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( candidate );
		state.PreviousPoint = state.Source.Point;
		state.Input = input;
		Reset( state.Source, candidate );
		if ( !_eligibility.Allows( input.Velocity, input.Normal ) )
			return false;

		_source.Update( chunks, new PhysicalHandSourceInput(
			input.Origin, input.Velocity, input.Normal, input.RequestedSide ), state.Source );
		candidate.Pending = 1;
		if ( !state.Source.Enabled )
			return false;

		_plan.Build( state.Source.Edge.Segment, state.Source.Point, _reach, false, reference );
		return _plan.Count != 0;
	}

	internal static void Reset( PhysicalHandSourceState source, PhysicalHandCandidateState candidate )
	{
		source.Enabled = false;
		source.Point = Vector3.Zero;
		source.Side = 0;
		candidate.Point = Vector3.Zero;
		candidate.Flags = ((ulong)BitConverter.SingleToUInt32Bits( float.MaxValue ) << 32)
			| ((uint)candidate.Flags & 0x4fffffffu);
		candidate.OppositeSide = Vector3.Zero;
		candidate.StartTime = -1f;
		candidate.ApexTime = -1f;
		candidate.Metric = new Vector4( -1f );
		candidate.SideSign = 0f;
	}
}
