namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector3 = System.Numerics.Vector3;

/// <summary>Prepares generated hand candidates and binds their retained state to rig pin updates.</summary>
internal sealed class PhysicalHandInteractionPipeline
{
	private readonly PhysicalHandCandidatePipeline _preparation;
	private readonly PhysicalHandInteractionController _interaction;

	internal PhysicalHandInteractionPipeline( PhysicalHandCandidatePipeline preparation,
		PhysicalHandInteractionController interaction )
	{
		ArgumentNullException.ThrowIfNull( preparation );
		ArgumentNullException.ThrowIfNull( interaction );
		_preparation = preparation;
		_interaction = interaction;
	}

	internal bool Prepare( GrindEdgeChunk[] chunks, MeshQueryInstance[] instances,
		PhysicalHandCandidatePipelineState state, PhysicalHandRequestInput input, Vector3 reference, int filter ) =>
		_preparation.Prepare( chunks, instances, state, input, reference, filter );

	internal void Update( PhysicalHandCandidatePipelineState state, PhysicalHandContactState contact,
		EffectorRigState rig, EffectorPoseCache cache, bool[] pinning, PhysicalHandInteractionContext context )
	{
		ArgumentNullException.ThrowIfNull( state );
		_interaction.Update( state.Candidate, contact, rig, cache, pinning, state.Surface,
			new PhysicalHandInteractionInput( context.ActionFlags, context.RigFlags,
				context.ModelToWorld, state.Request.CandidateInput ) );
	}
}
