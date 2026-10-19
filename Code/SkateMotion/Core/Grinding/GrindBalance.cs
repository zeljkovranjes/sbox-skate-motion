namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

internal static class GrindBalance
{
	/// <summary>Estimated largest rider lean over the rail, in radians, at full steering input.</summary>
	internal const float MaximumLean = 0.35f;

	/// <summary>Deck-up direction the rider balances toward: rail-perpendicular world up, leaned by steering.</summary>
	internal static Vector3 Target( GrindLock grind, ControlInput input )
	{
		var lean = Quaternion.CreateFromAxisAngle( grind.Direction, input.Steer * MaximumLean );
		return Vector3.Normalize( Vector3.Transform( grind.Up, lean ) );
	}
}
