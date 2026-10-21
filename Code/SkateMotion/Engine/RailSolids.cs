using Sandbox;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Engine;

/// <summary>Rail solids (metres) from the scene: a box collider's box, or a thin rail below a grind line.</summary>
internal static class RailSolids
{
	/// <summary>Half the size (inches) of the square rail a grind line without a box collider is taken to be.</summary>
	internal const float LineHalfSize = 0.5f;

	/// <summary>A box collider's box, turned so its local X runs along its longer side, as its grind line does.</summary>
	internal static RailSolid Of( BoxCollider box )
	{
		var world = box.WorldTransform;
		var half = box.Scale * 0.5f * world.Scale;
		var rotation = world.Rotation;
		if ( half.y > half.x )
		{
			rotation *= Rotation.FromYaw( 90f );
			half = new Vector3( half.y, half.x, half.z );
		}
		return new RailSolid( SkateUnits.Metres( world.PointToWorld( box.Center ) ), SkateUnits.ToCore( rotation ),
			SkateUnits.Metres( half ) );
	}

	/// <summary>A square rail <see cref="LineHalfSize"/> across, its top along the line from start to end (world inches).</summary>
	internal static RailSolid Below( Vector3 start, Vector3 end )
	{
		var along = end - start;
		var rotation = Rotation.LookAt( along.Normal, Vector3.Up );
		var centre = (start + end) * 0.5f - rotation.Up * LineHalfSize;
		return new RailSolid( SkateUnits.Metres( centre ), SkateUnits.ToCore( rotation ),
			SkateUnits.Metres( new Vector3( along.Length * 0.5f, LineHalfSize, LineHalfSize ) ) );
	}
}
