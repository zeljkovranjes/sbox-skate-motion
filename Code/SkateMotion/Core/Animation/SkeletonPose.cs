namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Local bone transforms for one skeleton, reused every frame.</summary>
internal sealed class SkeletonPose
{
	private readonly AnimationBoneTransform[] _sampled;

	internal SkeletonPose( int bones )
	{
		Positions = new Vector3[ bones ];
		Rotations = new Quaternion[ bones ];
		BoneWeights = new float[ bones ];
		Scales = new Vector4[ bones ];
		_sampled = new AnimationBoneTransform[ bones ];
		Array.Fill( BoneWeights, 1f );
		Array.Fill( Scales, Vector4.One );
	}

	internal Vector3[] Positions { get; }
	internal Quaternion[] Rotations { get; }
	internal float[] BoneWeights { get; }
	internal Vector4[] Scales { get; }

	internal void SetRest( ClipSkeleton skeleton )
	{
		skeleton.RestPositions.CopyTo( Positions, 0 );
		skeleton.RestRotations.CopyTo( Rotations, 0 );
		Array.Fill( BoneWeights, 1f );
		Array.Fill( Scales, Vector4.One );
	}

	internal void Sample( AnimationClip clip, ClipSkeleton skeleton, float time, bool looping )
	{
		if ( clip.RawPose is not null )
		{
			Sample( clip.RawPose, clip.RawSampleTime( time, looping ), true );
			clip.ApplyBoneWeightOverride( BoneWeights );
			return;
		}
		clip.Sample( skeleton, time, looping, Positions, Rotations );
		clip.SampleBoneWeights( BoneWeights );
		Array.Fill( Scales, Vector4.One );
	}

	/// <summary>Samples referenced raw keys while retaining masks and all scale lanes.</summary>
	internal void Sample( ReferencedAnimationClip clip, float time, bool interpolate )
	{
		if ( clip.Clip.BoneCount != Positions.Length )
			throw new ArgumentException( "The clip and pose need matching bone counts.", nameof( clip ) );
		clip.Sample( time, interpolate, _sampled );
		for ( var bone = 0; bone < Positions.Length; bone++ )
			SetTransform( bone, _sampled[ bone ] );
	}

	/// <summary>Samples raw locals or interval motion, retaining every transform lane.</summary>
	internal void Sample( RawAnimationClip clip, float previous, float current, uint wraps,
		bool interpolate, bool trajectoryEnabled )
	{
		if ( clip.BoneCount != Positions.Length )
			throw new ArgumentException( "The clip and pose need matching bone counts.", nameof( clip ) );
		if ( trajectoryEnabled )
			clip.SampleInterval( previous, current, unchecked( (int)wraps ), interpolate, _sampled );
		else
			clip.Sample( current, interpolate, _sampled );
		for ( var bone = 0; bone < Positions.Length; bone++ )
			SetTransform( bone, _sampled[ bone ] );
	}

	/// <summary>Samples a keyed clip or, for a blend clip, its blend under the parameters.</summary>
	internal void Sample( AnimationClip clip, ClipSkeleton skeleton, float time, bool looping, AnimationParameters parameters )
	{
		if ( clip.Blend is not null )
			clip.Blend.Sample( skeleton, parameters, time, looping, this );
		else
			Sample( clip, skeleton, time, looping );
	}

	internal void CopyFrom( SkeletonPose other )
	{
		other.Positions.CopyTo( Positions, 0 );
		other.Rotations.CopyTo( Rotations, 0 );
		other.BoneWeights.CopyTo( BoneWeights, 0 );
		other.Scales.CopyTo( Scales, 0 );
	}

	internal void SetTransforms( ReadOnlySpan<AnimationBoneTransform> transforms )
	{
		if ( transforms.Length != Positions.Length )
			throw new ArgumentException( "The source and pose need matching bone counts.", nameof( transforms ) );
		for ( var bone = 0; bone < transforms.Length; bone++ )
			SetTransform( bone, transforms[ bone ] );
	}

	internal void ComposeWith( SkeletonPose other, bool reverse, bool retainFirstTranslationW )
	{
		if ( other.Positions.Length != Positions.Length )
			throw new ArgumentException( "Composed poses need matching bone counts.", nameof( other ) );
		for ( var bone = 0; bone < Positions.Length; bone++ )
			SetTransform( bone, AnimationPoseComposer.Compose(
				Transform( bone ), other.Transform( bone ), reverse, retainFirstTranslationW ) );
	}

	/// <summary>Blends local transforms with ordered interpolation and normalized linear rotations.</summary>
	internal void BlendToward( SkeletonPose target, float weight )
	{
		for ( var i = 0; i < Positions.Length; i++ )
		{
			SetTransform( i, AnimationPoseBlender.Blend( Transform( i ), target.Transform( i ), weight ) );
		}
	}

	/// <summary>Blends each bone using its selected authored mask and retains the blended mask.</summary>
	internal void BlendWeightedToward( SkeletonPose target, float weight, bool maskFromFirst )
	{
		if ( target.Positions.Length != Positions.Length )
			throw new ArgumentException( "Weighted poses need matching bone counts.", nameof( target ) );

		for ( var i = 0; i < Positions.Length; i++ )
		{
			var mask = maskFromFirst ? BoneWeights[ i ] : target.BoneWeights[ i ];
			SetTransform( i, AnimationPoseBlender.BlendWithMask(
				Transform( i ), target.Transform( i ), weight, mask ) );
		}
	}

	internal AnimationBoneTransform Transform( int index ) => new(
		new Vector4( Positions[ index ], BoneWeights[ index ] ), Rotations[ index ], Scales[ index ] );

	internal void BlendMany( ReadOnlySpan<SkeletonPose> inputs, ReadOnlySpan<float> weights )
	{
		if ( inputs.Length < 1 || inputs.Length > AnimationPoseMultiBlender.MaximumInputs
			|| inputs.Length != weights.Length )
			throw new ArgumentException( "Blending needs one through five poses with matching weights." );
		for ( var index = 0; index < inputs.Length; index++ )
		{
			ArgumentNullException.ThrowIfNull( inputs[ index ] );
			if ( inputs[ index ].Positions.Length != Positions.Length )
				throw new ArgumentException( "Blended poses need matching bone counts.", nameof( inputs ) );
		}

		Span<AnimationBoneTransform> transforms = stackalloc AnimationBoneTransform[ AnimationPoseMultiBlender.MaximumInputs ];
		for ( var bone = 0; bone < Positions.Length; bone++ )
		{
			for ( var index = 0; index < inputs.Length; index++ )
				transforms[ index ] = inputs[ index ].Transform( bone );
			SetTransform( bone, AnimationPoseMultiBlender.Blend( transforms[ ..inputs.Length ], weights ) );
		}
	}

	private void SetTransform( int index, AnimationBoneTransform value )
	{
		Positions[ index ] = new Vector3( value.Position.X, value.Position.Y, value.Position.Z );
		Rotations[ index ] = value.Rotation;
		BoneWeights[ index ] = value.Position.W;
		Scales[ index ] = value.Scale;
	}
}
