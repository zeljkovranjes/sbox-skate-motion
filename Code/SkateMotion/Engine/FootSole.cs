using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.Animation;
using NumericsVector = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace SkateMotion.Engine;

/// <summary>
/// Points through a foot (heel to toe, edge to edge, sole to instep), measured once from the rider's own mesh at rest
/// and kept in the ankle bone's frame, so the whole shoe (not just the ankle and ball) can be kept out of a board at
/// any angle, including a board standing on edge during a flip.
/// </summary>
internal static class FootSole
{
	// Fractions of the ankle-to-ball length: behind the ankle to past the ball, and to each side.
	private static readonly float[] Along = { -0.55f, -0.3f, 0f, 0.5f, 1f, 1.35f };
	private static readonly float[] Across = { -0.45f, -0.22f, 0f, 0.22f, 0.45f };

	// Heights through the shoe between its sole and its top, as fractions.
	private static readonly float[] Through = { 0f, 0.5f, 1f };

	/// <returns>Foot points in the ankle's bind frame (retargeting units), or empty when the mesh cannot be traced.</returns>
	internal static NumericsVector[] Measure( Model model, PoseRetargeter retargeter, int ankle, int ball )
	{
		var ankleBone = model.Bones.AllBones.FirstOrDefault( bone => bone.Index == ankle );
		var ballBone = model.Bones.AllBones.FirstOrDefault( bone => bone.Index == ball );
		if ( ankleBone is null || ballBone is null || ankle == ball )
			return [];
		var from = ankleBone.LocalTransform.Position;
		var to = ballBone.LocalTransform.Position;
		var forward = (to - from).WithZ( 0f );
		var length = forward.Length;
		if ( length < 0.01f )
			return [];
		forward /= length;
		var side = Vector3.Cross( Vector3.Up, forward );
		var below = model.Bounds.Mins.z - 1f;
		var inverseFacing = NumericsQuaternion.Conjugate( retargeter.Facing );
		var bindPosition = retargeter.BindPosition( ankle );
		var bindRotation = NumericsQuaternion.Conjugate( retargeter.BindRotation( ankle ) );
		var points = new List<NumericsVector>();
		foreach ( var along in Along )
		foreach ( var across in Across )
		{
			var column = from + forward * (along * length) + side * (across * length);
			// Upward from below the model: the first face met is the outside of the sole.
			var sole = model.Trace.Ray( column.WithZ( below ), column.WithZ( from.z ) ).Run();
			if ( !sole.Hit )
				continue;
			// Downward from the ankle's height: the first face met is the top of the shoe, except around the ankle
			// itself where the leg begins; there the shoe is taken as reaching most of the way up to the ankle.
			var height = from.z - sole.HitPosition.z;
			var instep = model.Trace.Ray( column.WithZ( from.z ), sole.HitPosition + Vector3.Up * 0.05f ).Run();
			var top = instep.Hit && instep.HitPosition.z > sole.HitPosition.z + height * 0.15f
				? instep.HitPosition.z : sole.HitPosition.z + height * 0.75f;
			foreach ( var through in Through )
			{
				var point = sole.HitPosition.WithZ( sole.HitPosition.z + (top - sole.HitPosition.z) * through );
				var core = NumericsVector.Transform( SkateUnits.ToCore( point ), inverseFacing )
					- new NumericsVector( 0f, 0f, retargeter.Ground );
				points.Add( NumericsVector.Transform( core - bindPosition, bindRotation ) );
			}
		}
		return points.ToArray();
	}
}
