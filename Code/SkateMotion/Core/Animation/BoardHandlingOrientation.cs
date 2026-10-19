namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Publishes handling angles from completed physical frame and canonical spine observations.</summary>
internal static class BoardHandlingOrientation
{
	internal static BoardHandlingInput Publish( in BoardHandlingInput input, LimbRigidTransform processedFrame,
		Vector3 spineWorldPosition, Vector3 processedBoardPosition, int boardState,
		Vector3 retainedRetrievalPosition, LimbArcTangent arcTangent )
	{
		ArgumentNullException.ThrowIfNull( arcTangent );
		var spineDelta = spineWorldPosition - processedFrame.Translation;
		var height = Dot( spineDelta, processedFrame.Rotation.Y );
		// The height offset is rounded before addition, independently of the frame's translation.
		var origin = processedFrame.Translation + Scale( processedFrame.Rotation.Y, height );
		var point = boardState == 3 ? retainedRetrievalPosition : processedBoardPosition;
		var delta = point - origin;
		var yaw = arcTangent.Evaluate( Dot( delta, processedFrame.Rotation.X ),
			Dot( delta, processedFrame.Rotation.Z ) );
		return input with { YawRadians = yaw, PitchRadians = 0f };
	}
}
