using System.Numerics;
using SkateMotion.Core.BoardModel;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardPartDetectorTests
{
	private const float Millimetre = 0.001f;

	[Fact]
	public void TrucksAndWheelsWithRoomUnderTheDeckLeaveItItsFullLean()
	{
		var layout = BoardPartDetector.Detect( SyntheticBoard.Build().Mesh(), out var problem );

		Assert.NotNull( layout );
		Assert.True( layout.LeanLimit >= DeckLean.MaximumRoll, $"{layout.LeanLimit} rad, {problem}" );
	}

	[Fact]
	public void TrucksUpAgainstTheDeckAreBoltedToItAndLeanWithIt()
	{
		// Hinges reaching the deck's underside lean with it, so only the wheels (34 mm under the deck) limit its lean.
		var layout = BoardPartDetector.Detect( SyntheticBoard.BuildRigid().Mesh(), out var problem );

		Assert.NotNull( layout );
		Assert.True( layout.BoltedTrucks[ 0 ] && layout.BoltedTrucks[ 1 ], problem );
		Assert.True( layout.LeanLimit >= DeckLean.MaximumRoll, $"{layout.LeanLimit} rad" );
	}

	[Fact]
	public void WheelsCloseUnderTheDeckLimitItsLeanAndTheirRise()
	{
		// The deck 4 mm over the wheels' tops, with 2.4 mm hidden in it: the wheels have 6.4 mm of room, which a lean of
		// 6.4 / 82.5 radians (4.4 degrees) takes up at their inner faces, 82.5 mm out (their outer ones are past the deck).
		var layout = BoardPartDetector.Detect( SyntheticBoard.BuildRigid( deckDrop: 0.03f ).Mesh(), out var problem );

		Assert.NotNull( layout );
		Assert.InRange( layout.WheelRoom, 0.0055f, 0.0075f );
		Assert.InRange( layout.LeanLimit, 0.07f, 0.085f );
		Assert.True( layout.BoltedTrucks[ 0 ] && layout.BoltedTrucks[ 1 ] );
	}

	[Fact]
	public void SeparatePiecesAreFoundWholeAndMeasured()
	{
		var board = SyntheticBoard.Build();
		var layout = Detect( board.Mesh() );
		Assert.Equal( BoardPartSource.Pieces, layout.Source );
		AssertMeasured( layout );
		Assert.All( board.Triangles( "deck" ), t => Assert.Equal( BoardPart.Deck, layout.Parts[ t ] ) );
		for ( var wheel = 0; wheel < 4; wheel++ )
			Assert.All( board.Triangles( $"wheel {wheel}" ), t => Assert.Equal( BoardLayout.Wheel( wheel ), layout.Parts[ t ] ) );
		Assert.All( board.Triangles( "hanger front" ), t => Assert.Equal( BoardPart.FrontTruck, layout.Parts[ t ] ) );
		Assert.All( board.Triangles( "hanger rear" ), t => Assert.Equal( BoardPart.RearTruck, layout.Parts[ t ] ) );
		Assert.All( board.Triangles( "baseplate front" ), t => Assert.Equal( BoardPart.FrontBaseplate, layout.Parts[ t ] ) );
		Assert.All( board.Triangles( "baseplate rear" ), t => Assert.Equal( BoardPart.RearBaseplate, layout.Parts[ t ] ) );
		Assert.True( layout.Baseplates[ 0 ] && layout.Baseplates[ 1 ] );
		// The wheels' inner faces are a track less a wheel's width apart; the hanger reaches 1 cm under the axle.
		Assert.Equal( SyntheticBoard.Track - SyntheticBoard.WheelWidth, layout.InnerWheelGap, 4 );
		Assert.Equal( 0.01f, layout.HangerRadius, 4 );
	}

	[Fact]
	public void OneWeldedMeshIsSplitByWhereEachTriangleLies()
	{
		var board = SyntheticBoard.Build( welded: true );
		var layout = Detect( board.Mesh() );
		Assert.Equal( BoardPartSource.Clusters, layout.Source );
		AssertMeasured( layout, tolerance: 3f * Millimetre );
		Assert.All( board.Triangles( "deck" ), t => Assert.Equal( BoardPart.Deck, layout.Parts[ t ] ) );
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			var triangles = board.Triangles( $"wheel {wheel}" );
			Assert.True( Share( layout, triangles, BoardLayout.Wheel( wheel ) ) >= 0.95f, $"wheel {wheel}" );
		}
		Assert.True( Share( layout, board.Triangles( "hanger front" ), BoardPart.FrontTruck ) >= 0.75f );
		Assert.True( Share( layout, board.Triangles( "hanger rear" ), BoardPart.RearTruck ) >= 0.75f );
		// A baseplate welded to the rest still stays with the deck, as part of it or as its own baseplate.
		foreach ( var t in board.Triangles( "baseplate front" ) )
			Assert.Contains( layout.Parts[ t ], new[] { BoardPart.Deck, BoardPart.FrontBaseplate } );
	}

	[Fact]
	public void SeveralMeshesWithWheelsWeldedToTheirTrucksAreSplit()
	{
		// A deck of its own and each truck with its wheels as one piece: those pieces are split, the deck kept whole.
		var board = SyntheticBoard.Build( wheelsOnTrucks: true );
		var layout = Detect( board.Mesh() );
		Assert.Equal( BoardPartSource.Clusters, layout.Source );
		AssertMeasured( layout, tolerance: 3f * Millimetre );
		Assert.All( board.Triangles( "deck" ), t => Assert.Equal( BoardPart.Deck, layout.Parts[ t ] ) );
		Assert.All( board.Triangles( "baseplate rear" ), t => Assert.Equal( BoardPart.RearBaseplate, layout.Parts[ t ] ) );
		for ( var wheel = 0; wheel < 4; wheel++ )
			Assert.True( Share( layout, board.Triangles( $"wheel {wheel}" ), BoardLayout.Wheel( wheel ) ) >= 0.95f, $"wheel {wheel}" );
		Assert.True( Share( layout, board.Triangles( "hanger rear" ), BoardPart.RearTruck ) >= 0.75f );
	}

	[Theory]
	[InlineData( 0f, 0f, 90f )]
	[InlineData( 180f, 0f, 0f )]
	[InlineData( -90f, 90f, 0f )]
	public void AnyOrientationIsTurnedBackOntoTheBoard( float rollDegrees, float pitchDegrees, float yawDegrees )
	{
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, yawDegrees * MathF.PI / 180f )
			* Quaternion.CreateFromAxisAngle( Vector3.UnitY, pitchDegrees * MathF.PI / 180f )
			* Quaternion.CreateFromAxisAngle( Vector3.UnitX, rollDegrees * MathF.PI / 180f );
		var board = SyntheticBoard.Build();
		var layout = Detect( board.Mesh( turn ) );
		AssertMeasured( layout, symmetric: true );
		// The deck's top in the model's frame turns back onto the board's +Z.
		var top = Vector3.Transform( new Vector3( 0f, 0f, SyntheticBoard.RideHeight + SyntheticBoard.DeckThickness * 0.5f ), turn );
		Assert.Equal( SyntheticBoard.DeckThickness * 0.5f, layout.ToBoard( top ).Z, 3 );
	}

	[Fact]
	public void ModelUnitsScaleTheMeasurements()
	{
		var layout = Detect( SyntheticBoard.Build().Mesh( scale: 1.25f ) );
		Assert.Equal( SyntheticBoard.Wheelbase * 1.25f, layout.Wheelbase, 3 );
		Assert.Equal( SyntheticBoard.Radius * 1.25f, layout.WheelRadius, 3 );
		Assert.Equal( SyntheticBoard.RideHeight * 1.25f, layout.RideHeight, 3 );
	}

	[Fact]
	public void BonesNamedForWheelsAndTrucksMoveThem()
	{
		var hints = new List<BoardHint> { new( "root", Vector3.Zero, 0 ), new( "deck", new Vector3( 0f, 0f, SyntheticBoard.RideHeight ), 1 ) };
		var names = new[] { "wheel_fl", "wheel_fr", "wheel_rl", "wheel_rr" };
		for ( var wheel = 0; wheel < 4; wheel++ )
			hints.Add( new BoardHint( names[ wheel ], ModelPoint( SyntheticBoard.WheelCentre( wheel ) ), 2 + wheel ) );
		hints.Add( new BoardHint( "truck_front", new Vector3( SyntheticBoard.Wheelbase * 0.5f, 0f, 0.05f ), 6 ) );
		hints.Add( new BoardHint( "Hanger.Rear", new Vector3( -SyntheticBoard.Wheelbase * 0.5f, 0f, 0.05f ), 7 ) );
		var layout = Detect( SyntheticBoard.Build().Mesh( hints: hints.ToArray() ) );
		Assert.Equal( BoardPartSource.Bones, layout.Source );
		Assert.Equal( 1, layout.PartBones[ (int)BoardPart.Deck ] );
		for ( var wheel = 0; wheel < 4; wheel++ )
			Assert.Equal( 2 + wheel, layout.PartBones[ (int)BoardLayout.Wheel( wheel ) ] );
		Assert.Equal( 6, layout.PartBones[ (int)BoardPart.FrontTruck ] );
		Assert.Equal( 7, layout.PartBones[ (int)BoardPart.RearTruck ] );
	}

	[Fact]
	public void UnnamedBonesAtTheWheelsMoveThemAndTheFirstBoneTheDeck()
	{
		var hints = new List<BoardHint> { new( "bone0", Vector3.Zero, 0 ) };
		for ( var wheel = 0; wheel < 4; wheel++ )
			hints.Add( new BoardHint( $"bone{wheel + 1}", ModelPoint( SyntheticBoard.WheelCentre( wheel ) ) + new Vector3( 0f, 0.002f, 0f ), wheel + 1 ) );
		var layout = Detect( SyntheticBoard.Build().Mesh( hints: hints.ToArray() ) );
		Assert.Equal( BoardPartSource.Bones, layout.Source );
		Assert.Equal( 0, layout.PartBones[ (int)BoardPart.Deck ] );
		for ( var wheel = 0; wheel < 4; wheel++ )
			Assert.Equal( wheel + 1, layout.PartBones[ (int)BoardLayout.Wheel( wheel ) ] );
		Assert.Equal( -1, layout.PartBones[ (int)BoardPart.FrontTruck ] );
	}

	[Fact]
	public void AttachmentsAtTheWheelsGiveTheirCentres()
	{
		var shifted = ModelPoint( SyntheticBoard.WheelCentre( 1 ) ) + new Vector3( 0.004f, 0f, 0f );
		var layout = Detect( SyntheticBoard.Build().Mesh( hints: new[] { new BoardHint( "wheel_front_right", shifted, -1 ) } ) );
		Assert.Equal( SyntheticBoard.WheelCentre( 1 ).X + 0.004f, layout.WheelCentres[ 1 ].X, 4 );
		Assert.NotEqual( BoardPartSource.Bones, layout.Source );
	}

	[Fact]
	public void SomethingThatIsNotABoardIsRefusedWithAReason()
	{
		var cube = SyntheticCube();
		Assert.Null( BoardPartDetector.Detect( cube, out var problem ) );
		Assert.False( string.IsNullOrEmpty( problem ) );
		Assert.Null( BoardPartDetector.Detect( new BoardMesh( new Vector3[ 3 ], new[] { 0, 1, 2 } ), out problem ) );
		Assert.Contains( "too few", problem );
	}

	[Fact]
	public void TheDeckShapeGivesItsTopAndUnderside()
	{
		var board = SyntheticBoard.Build();
		var mesh = board.Mesh();
		var layout = Detect( mesh );
		var shape = DeckShape.Of( mesh, layout );
		var surface = shape.Surface( 0.1f, 0.05f );
		Assert.NotNull( surface );
		Assert.Equal( SyntheticBoard.DeckThickness * 0.5f, surface.Value.Top, 4 );
		Assert.Equal( -SyntheticBoard.DeckThickness * 0.5f, surface.Value.Bottom, 4 );
		// Trucks and wheels are not part of the deck, so under a truck the deck's own underside still answers.
		Assert.Equal( -SyntheticBoard.DeckThickness * 0.5f, shape.Surface( SyntheticBoard.Wheelbase * 0.5f, 0f ).Value.Bottom, 4 );
		Assert.Null( shape.Surface( 0.5f, 0f ) );
		Assert.Null( shape.Surface( 0f, 0.2f ) );
	}

	private static BoardLayout Detect( BoardMesh mesh )
	{
		var layout = BoardPartDetector.Detect( mesh, out var problem );
		Assert.True( layout is not null, problem );
		return layout;
	}

	private static void AssertMeasured( BoardLayout layout, float tolerance = Millimetre, bool symmetric = false )
	{
		Assert.InRange( layout.Wheelbase, SyntheticBoard.Wheelbase - tolerance, SyntheticBoard.Wheelbase + tolerance );
		Assert.InRange( layout.Track, SyntheticBoard.Track - tolerance, SyntheticBoard.Track + tolerance );
		Assert.InRange( layout.WheelRadius, SyntheticBoard.Radius - tolerance, SyntheticBoard.Radius + tolerance );
		Assert.InRange( layout.RideHeight, SyntheticBoard.RideHeight - tolerance, SyntheticBoard.RideHeight + tolerance );
		Assert.InRange( layout.Width, SyntheticBoard.DeckWidth - tolerance, SyntheticBoard.DeckWidth + tolerance );
		Assert.InRange( layout.Length, SyntheticBoard.DeckLength - tolerance, SyntheticBoard.DeckLength + tolerance );
		Assert.InRange( layout.Thickness, SyntheticBoard.DeckThickness - tolerance, SyntheticBoard.DeckThickness + tolerance );
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			var expected = SyntheticBoard.WheelCentre( wheel );
			var found = layout.WheelCentres[ wheel ];
			// A board's nose and tail look alike: turned end for end, front and rear (and so left and right) swap.
			if ( symmetric && found.X * expected.X < 0f )
				expected = new Vector3( -expected.X, -expected.Y, expected.Z );
			Assert.True( Vector3.Distance( expected, found ) <= tolerance * 2f, $"wheel {wheel}: {found} against {expected}" );
		}
		Assert.InRange( layout.PivotHeight, layout.WheelCentres[ 0 ].Z, -SyntheticBoard.DeckThickness * 0.5f );
	}

	private static float Share( BoardLayout layout, IReadOnlyList<int> triangles, BoardPart part ) =>
		triangles.Count( t => layout.Parts[ t ] == part ) / (float)triangles.Count;

	// A point of the board's frame (origin in the deck's middle) in the synthetic model's frame (origin on the ground).
	private static Vector3 ModelPoint( Vector3 board ) => board + new Vector3( 0f, 0f, SyntheticBoard.RideHeight );

	private static BoardMesh SyntheticCube()
	{
		var points = new List<Vector3>();
		for ( var i = 0; i < 8; i++ )
			points.Add( new Vector3( (i & 1) == 0 ? -0.3f : 0.3f, (i & 2) == 0 ? -0.3f : 0.3f, (i & 4) == 0 ? 0f : 0.6f ) );
		int[] faces = { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };
		var triangles = Enumerable.Range( 0, 3 ).SelectMany( copy => faces ).ToArray();
		return new BoardMesh( points.ToArray(), triangles );
	}
}
