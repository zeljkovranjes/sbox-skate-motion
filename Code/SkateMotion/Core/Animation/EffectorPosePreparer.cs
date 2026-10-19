namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Prepares the four effector pose caches after mode and weight updates.</summary>
internal sealed class EffectorPosePreparer
{
	private readonly EffectorTargetSolver _targets;

	internal EffectorPosePreparer( EffectorTargetSolver targets )
	{
		ArgumentNullException.ThrowIfNull( targets );
		_targets = targets;
	}

	internal void Update( EffectorPoseInput[] inputs, EffectorPoseState[] states, LimbRigidTransform root,
		LimbRigidTransform modelToWorld, LimbRigidTransform worldToModel, Vector3 bounds )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( states );
		if ( inputs.Length != 4 || states.Length != 4 )
			throw new ArgumentException( "Pose preparation needs four input and state slots." );
		var rootInverse = root.RigidInverse();
		for ( var i = 0; i < states.Length; i++ )
		{
			var input = inputs[ i ];
			var state = states[ i ] with { InsideBounds = false };
			var mode = state.TargetState.Mode;
			if ( mode is 2 or 3 )
			{
				var result = _targets.Solve( state.TargetState, input.Animated, input.Reference, modelToWorld, worldToModel );
				state = state with
				{
					TargetState = result.State,
					PinnedTarget = result.Target,
					PinnedReference = result.Reference ?? state.PinnedReference
				};
			}
			if ( mode == 1 || input.TargetWeight > 0f || input.OverrideWeight < 1f )
				state = Prepare( state, input, rootInverse, modelToWorld, bounds );
			states[ i ] = state;
		}
	}

	private static EffectorPoseState Prepare( EffectorPoseState state, EffectorPoseInput input,
		LimbRigidTransform rootInverse, LimbRigidTransform modelToWorld, Vector3 bounds )
	{
		var authored = rootInverse.Compose( input.Authored );
		var animated = modelToWorld.Compose( input.Animated );
		var position = authored.Translation;
		state = state with
		{
			AnimatedTarget = animated,
			AuthoredTarget = authored,
			LocalPosition = position,
			InsideBounds = !(-bounds.X > position.X || position.X > bounds.X
				|| -bounds.Y > position.Y || position.Y > bounds.Y
				|| -bounds.Z > position.Z || position.Z > bounds.Z)
		};
		if ( input.Reference.HasValue )
		{
			var relative = input.Animated.RigidInverse().Compose( input.Reference.Value );
			state = state with
			{
				AnimatedReference = animated.Compose( relative ),
				AuthoredReference = authored.Compose( relative )
			};
		}
		return state;
	}
}
