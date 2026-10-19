namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Transfers an authored prop pose to a retargeted hand in the clip frame.</summary>
internal static class RetargetedAttachment
{
	internal static (Vector3 Position, Quaternion Rotation) Place( Vector3 propPosition, Quaternion propRotation,
		Vector3 sourceHand, Vector3 targetHand, float scale, bool mirrored )
	{
		// Hand bone axes differ between models. Keep the authored prop orientation and wrist-to-prop
		// displacement instead of treating the target wrist's bind rotation as the prop's rotation.
		var offset = propPosition - sourceHand;
		if ( mirrored )
		{
			offset.Y = -offset.Y;
			propRotation = new Quaternion( -propRotation.X, propRotation.Y, -propRotation.Z, propRotation.W );
		}
		return (targetHand + offset * scale, propRotation);
	}
}
