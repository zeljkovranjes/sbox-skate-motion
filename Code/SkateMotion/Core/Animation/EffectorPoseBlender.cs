namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Blends four prepared pose caches and applies pin/release overrides.</summary>
internal sealed class EffectorPoseBlender
{
	private readonly LimbPoseBlender _poses;

	internal EffectorPoseBlender( LimbPoseBlender poses )
	{
		ArgumentNullException.ThrowIfNull( poses );
		_poses = poses;
	}

	internal void Update( EffectorPoseInput[] inputs, EffectorPoseState[] states,
		LimbRigidTransform root, LimbRigidTransform modelToWorld, LimbFrame authoredOrientation )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( states );
		if ( inputs.Length != 4 || states.Length != 4 )
			throw new ArgumentException( "Pose blending needs four input and state slots." );
		var frame = new LimbRigidTransform( authoredOrientation, modelToWorld.Transform( root.Translation ) );
		for ( var i = 0; i < states.Length; i++ )
		{
			var state = states[ i ];
			var mode = state.TargetState.Mode;
			if ( mode is not (1 or 2 or 3) )
				continue;
			var input = inputs[ i ];
			if ( input.TargetWeight > 0f )
			{
				state = state with
				{
					AnimatedTarget = BlendAuthored( state.AnimatedTarget, frame.Compose( state.AuthoredTarget ), input.TargetWeight, false )
				};
				if ( input.Reference.HasValue )
				{
					state = state with
					{
						AnimatedReference = BlendAuthored( state.AnimatedReference, frame.Compose( state.AuthoredReference ), input.TargetWeight, true )
					};
				}
			}
			if ( mode is 2 or 3 )
			{
				// Overrides update both caches even when the linked animated pose is absent.
				state = state with
				{
					AnimatedTarget = Override( state.AnimatedTarget, state.PinnedTarget, input.OverrideWeight ),
					AnimatedReference = Override( state.AnimatedReference, state.PinnedReference, input.OverrideWeight )
				};
			}
			states[ i ] = state;
		}
	}

	private LimbRigidTransform BlendAuthored( LimbRigidTransform animated, LimbRigidTransform authored,
		float weight, bool linked )
	{
		var left = new LimbRigidTransform( animated.Rotation.Orthonormalized(), Vector3.Zero );
		var right = new LimbRigidTransform( authored.Rotation.Orthonormalized(), Vector3.Zero );
		var result = _poses.Blend( left, right, weight ).Transform;
		var retained = Scale( animated.Translation, 1f - weight );
		// Primary and linked positions have distinct rounding at the final weighted addition.
		var position = linked ? AddScaled( authored.Translation, weight, retained )
			: Scale( authored.Translation, weight ) + retained;
		return result with { Translation = position };
	}

	private LimbRigidTransform Override( LimbRigidTransform animated, LimbRigidTransform pinned, float weight ) =>
		weight < 1f ? _poses.BlendTransform( animated, pinned, weight ) : pinned;
}
