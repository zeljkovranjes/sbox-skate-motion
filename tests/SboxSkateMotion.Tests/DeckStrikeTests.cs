using System.Numerics;
using SkateMotion.Core.BoardModel;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DeckStrikeTests
{
	private const float RideHeight = 0.1f;
	private const float Radius = 0.03f;
	private const float Wheelbase = 0.5f;
	private const float Thickness = 0.012f;

	[Fact]
	public void AFlatDeckCollidesAtItsOwnLength()
	{
		var shape = new DeckShape( Deck( 0.4f, 0.4f, 0f ) );
		Assert.Equal( 0.8f, shape.StrikeLength( Layout( 0.8f ), Thickness ), 3 );
	}

	[Fact]
	public void AKickedDeckCollidesAsAShorterFlatOneWhoseEndsMeetTheGroundAtTheSameTip()
	{
		// Flat to 0.3 m either side of the middle, then kicked up 45 mm by the ends at 0.45 m.
		var shape = new DeckShape( Deck( 0.3f, 0.45f, 0.045f ) );
		var length = shape.StrikeLength( Layout( 0.9f ), Thickness );
		// The kicked end's tip meets the ground first as the board tips onto its truck: 0.2 m beyond the axle and
		// 0.109 m above it, the wheels 0.03 m under the axle.
		var axle = Radius - RideHeight;
		var tip = Tip( 0.45f - Wheelbase * 0.5f, -Thickness * 0.5f + 0.045f - axle );
		var flat = Tip( length * 0.5f - Wheelbase * 0.5f, -Thickness * 0.5f - axle );
		Assert.InRange( length, 0.7f, 0.85f );
		Assert.Equal( tip, flat, 3 );
	}

	[Fact]
	public void ANoseWithoutUndersideBeyondItsAxleKeepsItsLength()
	{
		var shape = new DeckShape( Deck( 0.26f, 0.26f, 0f ) );
		Assert.Equal( 0.52f, shape.StrikeLength( Layout( 0.52f ), Thickness ), 4 );
	}

	private static float Tip( float along, float height ) =>
		MathF.Atan2( height, along ) + MathF.Asin( Radius / MathF.Sqrt( along * along + height * height ) );

	private static BoardLayout Layout( float length )
	{
		var axle = Radius - RideHeight;
		return new BoardLayout
		{
			WheelCentres = new[]
			{
				new Vector3( Wheelbase * 0.5f, 0.1f, axle ), new Vector3( Wheelbase * 0.5f, -0.1f, axle ),
				new Vector3( -Wheelbase * 0.5f, 0.1f, axle ), new Vector3( -Wheelbase * 0.5f, -0.1f, axle )
			},
			AxleCentres = new[] { new Vector3( Wheelbase * 0.5f, 0f, axle ), new Vector3( -Wheelbase * 0.5f, 0f, axle ) },
			WheelRadius = Radius,
			Length = length,
			Width = 0.2f,
			Thickness = Thickness,
			RideHeight = RideHeight
		};
	}

	// A deck's top and underside, flat to <paramref name="flat"/> either side of its middle and rising by
	// <paramref name="kick"/> from there to its ends at <paramref name="end"/>, 0.2 m wide; triangles in the board's frame.
	private static Vector3[] Deck( float flat, float end, float kick )
	{
		var corners = new List<Vector3>();
		float[] xs = { -end, -flat, flat, end };
		float Rise( float x ) => MathF.Abs( x ) <= flat ? 0f : kick * (MathF.Abs( x ) - flat) / MathF.Max( end - flat, 1e-6f );
		for ( var i = 0; i < xs.Length - 1; i++ )
		{
			var a = xs[ i ];
			var b = xs[ i + 1 ];
			if ( b - a < 1e-6f )
				continue;
			foreach ( var z in new[] { Thickness * 0.5f, -Thickness * 0.5f } )
			{
				var p0 = new Vector3( a, -0.1f, z + Rise( a ) );
				var p1 = new Vector3( b, -0.1f, z + Rise( b ) );
				var p2 = new Vector3( b, 0.1f, z + Rise( b ) );
				var p3 = new Vector3( a, 0.1f, z + Rise( a ) );
				corners.AddRange( new[] { p0, p1, p2, p0, p2, p3 } );
			}
		}
		return corners.ToArray();
	}
}
