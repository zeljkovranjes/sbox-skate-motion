using Sandbox;
using SkateMotion.Core;

namespace SkateMotion.Engine;

internal sealed class BoardRig
{
	// Share by which a measured size may differ from the profile's before the difference is reported.
	private const float Noticeable = 0.05f;

	internal Model DeckModel { get; init; }
	internal Model WheelModel { get; init; }
	internal GameObject[] Wheels { get; }
	internal Vector3[] Origins { get; } = new Vector3[ 4 ];
	internal Rotation[] WheelRotations { get; } = new Rotation[ 4 ];
	internal bool HasCustomWheels { get; }

	/// <summary>A board model taken apart, or null for the generated board or a custom deck.</summary>
	internal BoardModelParts Parts { get; }

	/// <summary>Bone each part is moved by (indexed by <see cref="Core.BoardModel.BoardPart"/>, -1 none) when the board
	/// model is drawn whole and moved by its bones.</summary>
	internal int[] PartBones { get; }

	/// <summary>The settings simulated: the profile's, with the board model's measured wheelbase, track and ride height.</summary>
	internal SkateSettings Settings { get; }

	/// <summary>Wheel radius simulated (metres): the board model's measured one, or the profile's.</summary>
	internal float WheelRadius { get; }

	/// <summary>How far (inches) below the deck's middle the line through the trucks' pivots runs: the deck leans about it.</summary>
	internal float PivotDepth { get; }

	/// <summary>
	/// The furthest the shown deck leans (radians): as far as the board model's own trucks and wheels leave it room to
	/// (BoardLayout.LeanLimit), unlimited for a generated board.
	/// </summary>
	internal float LeanLimit { get; }

	/// <summary>Whether each truck is bolted up against the deck and leans with it (BoardLayout.BoltedTrucks).</summary>
	internal bool[] BoltedTrucks { get; }

	/// <summary>How far (inches) the wheels can come up toward the level deck before they meet it (BoardLayout.WheelRoom).</summary>
	internal float WheelRoom { get; }

	/// <summary>How far (inches) from the deck's long middle its farthest wheel point under it lies (BoardLayout.WheelReach).</summary>
	internal float WheelReach { get; }

	internal BoardRig( GameObject board, SkateSettings settings, float radius, GameObject[] wheels = null, BoardModelParts parts = null,
		int[] partBones = null )
	{
		Wheels = wheels ?? new GameObject[ 4 ];
		var count = 0;
		foreach ( var wheel in Wheels )
			if ( wheel.IsValid() ) count++;
		if ( count != 0 && count != 4 )
			throw new ArgumentException( "Assign all four wheel objects, or leave all four empty." );
		HasCustomWheels = count == 4;
		Parts = parts;
		PartBones = partBones ?? parts?.Layout.PartBones;
		Settings = parts is null ? settings : Measured( board, settings, radius, parts );
		WheelRadius = parts?.Layout.WheelRadius ?? radius;
		LeanLimit = parts?.Layout.LeanLimit ?? float.PositiveInfinity;
		BoltedTrucks = parts?.Layout.BoltedTrucks ?? new bool[ 2 ];
		WheelRoom = (parts?.Layout.WheelRoom ?? float.PositiveInfinity) * SkateUnits.InchesPerMetre;
		WheelReach = (parts?.Layout.WheelReach ?? 0f) * SkateUnits.InchesPerMetre;
		// The trucks' pivots sit about where the hanger meets the baseplate, three fifths of the way up from the axle to
		// the deck's underside (the generated deck's is 7 mm below its middle).
		var axle = Settings.RideHeight - WheelRadius;
		PivotDepth = (parts is null ? axle - 0.6f * (axle - 0.007f) : -parts.Layout.PivotHeight) * SkateUnits.InchesPerMetre;
		for ( var i = 0; i < 4; i++ )
		{
			Origins[ i ] = parts is null ? SkateUnits.Inches( SkateWheels.Offset( i, settings ) )
				: parts.WheelCentre( i ) + Vector3.Up * (axle * SkateUnits.InchesPerMetre);
			WheelRotations[ i ] = Rotation.Identity;
			if ( !HasCustomWheels )
				continue;
			Origins[ i ] = board.WorldRotation.Inverse * (Wheels[ i ].WorldPosition - board.WorldPosition)
				+ Vector3.Up * (axle * SkateUnits.InchesPerMetre);
			WheelRotations[ i ] = board.WorldRotation.Inverse * Wheels[ i ].WorldRotation;
		}
	}

	// The profile's settings simulated at the board model's own size, with any notable difference reported.
	private static SkateSettings Measured( GameObject board, SkateSettings settings, float radius, BoardModelParts parts )
	{
		var layout = parts.Layout;
		// The deck's ends (kicked nose and tail too) rest on a rail in a slide; against the ground it collides at its own
		// width and thickness (no thinner than MinimumDeckThickness) and as long as a flat deck whose ends meet the ground
		// where its kicked nose and tail do; and a grinding truck sits on the rail by its own hanger and between its wheels.
		var thickness = MathF.Max( layout.Thickness, MinimumDeckThickness );
		var measured = settings with
		{
			Wheelbase = layout.Wheelbase, Track = layout.Track, RideHeight = layout.RideHeight,
			DeckLength = layout.Length, DeckWidth = layout.Width,
			DeckCollisionLength = parts.Shape?.StrikeLength( layout, thickness ) ?? layout.Length,
			DeckThickness = thickness,
			HangerRadius = layout.HangerRadius > 0f ? layout.HangerRadius : settings.HangerRadius,
			InnerWheelGap = layout.InnerWheelGap > 0f ? layout.InnerWheelGap : settings.InnerWheelGap
		};
		if ( Differs( layout.Wheelbase, settings.Wheelbase ) || Differs( layout.Track, settings.Track )
			|| Differs( layout.RideHeight, settings.RideHeight ) || Differs( layout.WheelRadius, radius ) )
			Log.Info( $"Skateboard {board.Name} rides on its model's measurements: wheelbase {layout.Wheelbase:F3} m "
				+ $"(profile {settings.Wheelbase:F3}), track {layout.Track:F3} m ({settings.Track:F3}), wheel radius "
				+ $"{layout.WheelRadius:F4} m ({radius:F4}), ride height {layout.RideHeight:F3} m ({settings.RideHeight:F3}); the deck "
				+ $"is {measured.DeckLength:F3} m long and collides {measured.DeckCollisionLength:F3} x {measured.DeckWidth:F3} x {measured.DeckThickness:F3} m, hangers {measured.HangerRadius * 1000f:F1} mm "
				+ $"below the axles, {measured.InnerWheelGap:F3} m between each truck's wheels." );
		measured.Validate();
		return measured;
	}

	// Thinnest deck (metres) the board collides as: a thinner one is measured from a model that leaves its grip or
	// its ply out, and passes further into a rail's edge in one step.
	private const float MinimumDeckThickness = 0.01f;

	private static bool Differs( float measured, float profile ) => MathF.Abs( measured - profile ) > profile * Noticeable;
}
