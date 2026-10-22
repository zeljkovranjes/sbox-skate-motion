namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// A skateboard model's parts and measurements, found from its own geometry. Places are in the board's frame: metres,
/// +X along the deck, +Y to its left, +Z up, the origin in the middle of the deck (halfway through it at its centre).
/// </summary>
internal sealed class BoardLayout
{
	/// <summary>Turns the model's frame into the board's (applied after moving <see cref="Origin"/> to zero).</summary>
	internal Quaternion Rotation { get; init; } = Quaternion.Identity;

	/// <summary>The middle of the deck in the model's frame (metres).</summary>
	internal Vector3 Origin { get; init; }

	/// <summary>The part each triangle belongs to.</summary>
	internal BoardPart[] Parts { get; init; }

	/// <summary>Wheel centres: front left, front right, rear left, rear right.</summary>
	internal Vector3[] WheelCentres { get; init; }

	/// <summary>Middle of each truck's axle, front then rear: where its hanger turns about the deck.</summary>
	internal Vector3[] AxleCentres { get; init; }

	internal float WheelRadius { get; init; }

	internal float WheelWidth { get; init; }

	/// <summary>The narrower of the two trucks' gaps between their wheels' inner faces: a rail ground on a truck stays in it.</summary>
	internal float InnerWheelGap { get; init; }

	/// <summary>How far the hanger reaches below its axle between the wheels: where a grinding truck rests on a rail.</summary>
	internal float HangerRadius { get; init; }

	/// <summary>Height of the trucks' pivots: where a deck leaning on its trucks turns about them.</summary>
	internal float PivotHeight { get; init; }

	/// <summary>
	/// The furthest the deck can lean on its trucks (radians) before it comes down through its own wheels or hangers, which
	/// stay level with the ground as it leans (a truck bolted to it leans with it); infinity when nothing of theirs lies
	/// under the deck.
	/// </summary>
	internal float LeanLimit { get; init; } = float.PositiveInfinity;

	/// <summary>
	/// Whether each truck, front then rear, is bolted up against the deck (a hinge, a hanger welded to its baseplate): it
	/// leans with the deck instead of staying level with the ground, and its wheels tilt with it.
	/// </summary>
	internal bool[] BoltedTrucks { get; init; } = new bool[ 2 ];

	/// <summary>
	/// How far (metres) the wheels can come up toward the deck, at the least, before they meet it with the deck level;
	/// infinity when no wheel lies under the deck.
	/// </summary>
	internal float WheelRoom { get; init; } = float.PositiveInfinity;

	/// <summary>
	/// How far (metres) from the deck's long middle the farthest wheel or free hanger point under the deck lies: a deck
	/// leaning past <see cref="LeanLimit"/> by an angle must rise by about that angle times this to clear them; 0 when
	/// none lies under the deck.
	/// </summary>
	internal float WheelReach { get; init; }

	/// <summary>The deck's length, nose to tail.</summary>
	internal float Length { get; init; }

	/// <summary>The deck's width at its middle.</summary>
	internal float Width { get; init; }

	/// <summary>The deck's thickness at its middle.</summary>
	internal float Thickness { get; init; }

	/// <summary>Height of the deck's middle above the ground the wheels stand on.</summary>
	internal float RideHeight { get; init; }

	/// <summary>Whether each truck's baseplate is its own piece of mesh, separate from the hanger.</summary>
	internal bool[] Baseplates { get; init; } = new bool[ 2 ];

	/// <summary>The bone that moves each part (indexed by <see cref="BoardPart"/>), or -1.</summary>
	internal int[] PartBones { get; init; } = NoBones();

	internal BoardPartSource Source { get; init; }

	/// <summary>Distance between the front and rear axles.</summary>
	internal float Wheelbase => (WheelCentres[ 0 ].X + WheelCentres[ 1 ].X - WheelCentres[ 2 ].X - WheelCentres[ 3 ].X) * 0.5f;

	/// <summary>Distance between the left and right wheels' centres.</summary>
	internal float Track => (WheelCentres[ 0 ].Y - WheelCentres[ 1 ].Y + WheelCentres[ 2 ].Y - WheelCentres[ 3 ].Y) * 0.5f;

	/// <summary>A point of the model's frame in the board's.</summary>
	internal Vector3 ToBoard( Vector3 model ) => Vector3.Transform( model - Origin, Rotation );

	/// <summary>Triangles of a part.</summary>
	internal int CountOf( BoardPart part )
	{
		var count = 0;
		foreach ( var each in Parts )
		{
			if ( each == part )
				count++;
		}
		return count;
	}

	internal static BoardPart Wheel( int index ) => BoardPart.FrontLeftWheel + index;

	internal static BoardPart Truck( int index ) => BoardPart.FrontTruck + index;

	internal static BoardPart Baseplate( int index ) => BoardPart.FrontBaseplate + index;

	internal static int[] NoBones()
	{
		var bones = new int[ BoardPart.RearRightWheel - BoardPart.Deck + 1 ];
		Array.Fill( bones, -1 );
		return bones;
	}
}
