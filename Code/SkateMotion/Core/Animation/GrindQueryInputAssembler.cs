namespace SkateMotion.Core.Animation;

/// <summary>Populates board-derived query fields while retaining caller overrides and external context.</summary>
internal static class GrindQueryInputAssembler
{
	private const ulong DirectionFanFlag = 1UL << 13;

	internal static GrindQueryRequestInput Assemble( GrindQueryRequestInput input,
		GrindQueryBoardState board, float spread, float spreadScale ) => input with
	{
		Velocity = board.Velocity,
		SecondaryVelocity = board.SecondaryVelocity,
		Count = (board.MotionFlags & DirectionFanFlag) != 0 ? (ushort)7 : (ushort)1,
		Spread = spread,
		SpreadScale = spreadScale,
		LeadTime = board.LeadTime,
		Reference = board.Reference,
		Point = board.Point,
		Up = new( board.WorldFrame.M21, board.WorldFrame.M22, board.WorldFrame.M23, board.WorldFrame.M24 ),
		LocalFrame = board.LocalFrame
	};
}
