namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Identifies a trajectory track and its authored cycle displacement.</summary>
internal readonly struct AnimationClipTrajectory
{
	internal AnimationClipTrajectory( int boneIndex, Vector3 translation, Quaternion rotation )
	{
		if ( boneIndex < -1 || !float.IsFinite( translation.X ) || !float.IsFinite( translation.Y )
			|| !float.IsFinite( translation.Z ) || !float.IsFinite( rotation.X )
			|| !float.IsFinite( rotation.Y ) || !float.IsFinite( rotation.Z ) || !float.IsFinite( rotation.W ) )
			throw new ArgumentException( "Trajectory metadata must use a valid bone index and finite components." );
		BoneIndex = boneIndex;
		Translation = translation;
		Rotation = rotation;
	}

	internal int BoneIndex { get; }
	internal Vector3 Translation { get; }
	internal Quaternion Rotation { get; }
}
