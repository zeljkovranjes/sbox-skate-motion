namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Converts two sampled trajectory transforms into a local motion delta.</summary>
internal static class AnimationPoseTrajectory
{
	internal static AnimationBoneTransform Delta( in AnimationBoneTransform previous,
		in AnimationBoneTransform current, in AnimationClipTrajectory trajectory, bool wrapped )
	{
		var cyclePosition = wrapped ? trajectory.Translation : Vector3.Zero;
		var cycleRotation = wrapped ? trajectory.Rotation : Quaternion.Identity;
		var inversePrevious = new Quaternion( -previous.Rotation.X, -previous.Rotation.Y,
			-previous.Rotation.Z, previous.Rotation.W );
		var position = Rotate( current.Rotation, cyclePosition );
		position += new Vector3( current.Position.X, current.Position.Y, current.Position.Z );
		position -= new Vector3( previous.Position.X, previous.Position.Y, previous.Position.Z );
		position = Rotate( inversePrevious, position );
		var rotation = AnimationPoseComposer.ComposeRotation( cycleRotation, current.Rotation );
		rotation = AnimationPoseComposer.ComposeRotation( rotation, inversePrevious );
		return new AnimationBoneTransform( new Vector4( position, current.Position.W ), rotation, current.Scale );
	}

	private static Vector3 Rotate( Quaternion rotation, Vector3 position )
	{
		var axis = new Vector3( rotation.X, rotation.Y, rotation.Z );
		var inner = AddScaled( position, rotation.W, Cross( axis, position ) );
		return AddScaled( Cross( axis, inner ), 2f, position );
	}
}
