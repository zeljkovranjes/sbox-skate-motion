namespace SkateMotion.Core.Animation;

/// <summary>Applies the four prepared effector poses through their skeleton chains.</summary>
internal sealed class EffectorRigSolver
{
	private readonly LimbChainSolver _limbs;

	internal EffectorRigSolver( LimbChainSolver limbs )
	{
		ArgumentNullException.ThrowIfNull( limbs );
		_limbs = limbs;
	}

	internal void Update( EffectorBoneBinding[] bindings, EffectorPoseState[] states, int[] parents,
		LimbRigidTransform[] bindTransforms, LimbRigidTransform[] animatedBones, LimbRigidTransform[] mutableBones,
		LimbRigidTransform worldToModel, float minimumDegrees, float maximumDegrees )
	{
		ArgumentNullException.ThrowIfNull( bindings );
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( parents );
		ArgumentNullException.ThrowIfNull( bindTransforms );
		ArgumentNullException.ThrowIfNull( animatedBones );
		ArgumentNullException.ThrowIfNull( mutableBones );
		if ( bindings.Length != 4 || states.Length != 4 || parents.Length != animatedBones.Length
			|| bindTransforms.Length != animatedBones.Length || mutableBones.Length != animatedBones.Length )
			throw new ArgumentException( "Rig solving needs four bindings/states and matching skeleton arrays." );
		for ( var i = 0; i < states.Length; i++ )
		{
			var state = states[ i ];
			if ( state.TargetState.Mode is not (1 or 2 or 3) )
				continue;
			var binding = bindings[ i ];
			ValidateIndex( binding.EndBone, parents.Length );
			if ( binding.ReferenceBone < -1 || !float.IsFinite( binding.MaximumReach ) || binding.MaximumReach < 0f )
				throw new ArgumentException( "Bone references and reach limits are invalid." );
			var linked = binding.ReferenceBone != -1;
			var chainEnd = linked ? binding.ReferenceBone : binding.EndBone;
			ValidateIndex( chainEnd, parents.Length );
			var lower = parents[ chainEnd ];
			ValidateIndex( lower, parents.Length );
			var upper = parents[ lower ];
			ValidateIndex( upper, parents.Length );

			var desired = worldToModel.Compose( state.AnimatedTarget );
			var desiredReference = linked ? worldToModel.Compose( state.AnimatedReference ) : LimbRigidTransform.Identity;
			var chainTarget = (linked ? desiredReference : desired).Compose( bindTransforms[ chainEnd ] );
			var ankle = animatedBones[ chainEnd ].Translation;
			var result = _limbs.Solve( animatedBones[ upper ].Translation, animatedBones[ lower ].Translation,
				ankle, chainTarget.Translation - ankle, mutableBones[ upper ], mutableBones[ lower ],
				minimumDegrees, maximumDegrees, binding.MaximumReach );
			if ( !result.Applied )
				continue;

			mutableBones[ upper ] = result.Upper;
			mutableBones[ lower ] = result.Lower;
			mutableBones[ binding.EndBone ] = desired with { Translation = desired.Translation + result.Residual };
			state = state with
			{
				AnimatedTarget = state.AnimatedTarget with { Translation = state.AnimatedTarget.Translation + result.Residual }
			};
			if ( linked )
			{
				mutableBones[ binding.ReferenceBone ] = desiredReference with { Translation = desiredReference.Translation + result.Residual };
				state = state with
				{
					AnimatedReference = state.AnimatedReference with { Translation = state.AnimatedReference.Translation + result.Residual }
				};
			}
			states[ i ] = state;
		}
	}

	private static void ValidateIndex( int index, int count )
	{
		if ( (uint)index >= (uint)count )
			throw new ArgumentException( "Active limb chain references a bone outside the skeleton." );
	}
}
