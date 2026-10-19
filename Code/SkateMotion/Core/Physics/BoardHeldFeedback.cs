namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Builds held-board feedback from the selected hand and reference positions.</summary>
internal static class BoardHeldFeedback
{
	internal static BoardContactFeedback Create( Vector3 boardPosition, Vector3 handPosition,
		Vector3 referencePosition, ulong bodyFlags ) => new(
		Direction( handPosition - boardPosition ), Direction( referencePosition - boardPosition ),
		.8f, true, bodyFlags | (1UL << 62) );

	private static Vector3 Direction( Vector3 difference )
	{
		var squared = Dot( difference, difference );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > .000001f ? Scale( difference, inverse ) : Vector3.Zero;
	}
}
