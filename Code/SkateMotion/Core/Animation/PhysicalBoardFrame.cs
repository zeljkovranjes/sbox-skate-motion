namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Produces the board frame and writes its physical target on every update.</summary>
internal static class PhysicalBoardFrame
{
	internal static BoardFrameCorrectionResult Update( BoardFrameCorrection correction,
		PhysicalBodyBinding target, LimbRigidTransform source, Vector4 orientation,
		byte correctionFlag, bool alternate )
	{
		ArgumentNullException.ThrowIfNull( correction );
		ArgumentNullException.ThrowIfNull( target );
		var result = correctionFlag != 0
			? correction.Update( source, orientation, correctionFlag, alternate )
			: new BoardFrameCorrectionResult( source, orientation, correctionFlag );
		target.Write( result.Frame );
		return result;
	}
}
