namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Builds owner-driven hand requests with retained side retries and explicit deactivation reset.</summary>
internal sealed class PhysicalHandOwnerController
{
	private readonly PhysicalHandInteractionPipeline _pipeline;
	private readonly int _retryLimit;

	internal PhysicalHandOwnerController( PhysicalHandInteractionPipeline pipeline, int retryLimit )
	{
		ArgumentNullException.ThrowIfNull( pipeline );
		_pipeline = pipeline;
		_retryLimit = retryLimit;
	}

	internal bool Prepare( GrindEdgeChunk[] chunks, MeshQueryInstance[] instances,
		PhysicalHandOwnerState owner, PhysicalHandCandidatePipelineState state,
		PhysicalHandContactState contact, PhysicalHandOwnerInput input, Vector3 reference, int filter )
	{
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( contact );
		if ( (input.ActionFlags & (1UL << 22)) == 0 )
		{
			PhysicalHandRequestController.Reset( state.Request.Source, state.Candidate );
			contact.Weight = 0f;
			contact.Radius = new Vector4( -1f );
			contact.Flags &= 0x3fffffffffffffffUL;
			return false;
		}

		var retainSide = (input.RigFlags & (1UL << 60)) != 0;
		if ( !retainSide || (input.RigFlags & (1UL << 61)) != 0 )
		{
			owner.RetryCount = 0;
			owner.RequestedSide = 0;
		}
		if ( retainSide && input.Normal.Y < .949999988079071f )
		{
			if ( owner.RequestedSide == 0 )
				owner.RequestedSide = state.Request.Source.Side;
			var frame = PhysicalHandSourceGeometry.BuildFrame( input.Origin, input.Normal ).RigidInverse();
			var velocity = frame.Rotation.Transform( input.Velocity, Vector3.Zero );
			var detected = velocity.X > 0f ? 1 : 2;
			owner.RetryCount = detected == owner.RequestedSide ? 0 : unchecked( owner.RetryCount + 1 );
			if ( owner.RetryCount > _retryLimit )
				owner.RequestedSide = 0;
		}
		else
		{
			owner.RequestedSide = 0;
		}

		return _pipeline.Prepare( chunks, instances, state, new PhysicalHandRequestInput(
			input.Origin, input.Velocity, input.Normal, input.Side, input.Tangent,
			owner.RequestedSide ), reference, filter );
	}
}
