using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleEdgeCodesTests
{
	// Floor (z = 0, x < 0) and a ramp face rising 20 degrees from x = 0; both meet along the Y axis.
	private static readonly Vector3[] A = { new( -1, -1, 0 ), new( -1, 1, 0 ), new( 0, -1, 0 ), new( 0, 1, 0 ) };
	private static readonly Vector3[] B = { new( 0, -1, 0 ), new( 0, -1, 0 ), Ramp( -1 ), Ramp( 1 ) };
	private static readonly Vector3[] C = { new( -1, 1, 0 ), new( 0, 1, 0 ), new( 0, 1, 0 ), Ramp( -1 ) };

	private static Vector3 Ramp( float y ) => new( MathF.Cos( 0.349f ), y, MathF.Sin( 0.349f ) );

	[Fact]
	public void SharedEdgesKnowTheirNeighbourAndOpenEdgesStayOpen()
	{
		var codes = TriangleEdgeCodes.Compute( A, B, C, 1e-4f );
		// Triangle 2 (A, B, C) = ((0,-1,0), ramp(-1), (0,1,0)): edge (C, A) is the fold with triangle 1.
		var fold = codes[ 2 * 3 + 2 ];
		Assert.Equal( 0, fold & TriangleEdgeCodes.Convex );
		Assert.True( TriangleEdgeCodes.Cosine( fold & 31 ) <= MathF.Cos( 0.349f ) + 1e-4f );
		Assert.True( TriangleEdgeCodes.Cosine( (fold & 31) + 1 ) > MathF.Cos( 0.349f ) );
		// Triangle 0's first edge (A, B) = ((-1,-1,0), (0,-1,0)) has no neighbour.
		Assert.Equal( TriangleEdgeCodes.Convex, codes[ 0 ] & 0x3f );
		// The floor diagonal is shared and flat: convex with the tightest code.
		var diagonal = codes[ 0 * 3 + 1 ];
		Assert.Equal( TriangleEdgeCodes.Convex, diagonal & TriangleEdgeCodes.Convex );
		Assert.True( TriangleEdgeCodes.Cosine( diagonal & 31 ) > 0.9999f );
	}

	[Fact]
	public void ASideNormalAtTheFoldIsRejectedOnlyWithEdgeCodes()
	{
		var codes = TriangleEdgeCodes.Compute( A, B, C, 1e-4f );
		// A wheel resting in the fold pushed back horizontally, the way a seam would read as a wall.
		var normal = Words( Vector3.Normalize( new Vector3( 1f, 0f, 0.05f ) ) );
		Assert.Equal( 0u, Filter( 2, codes.AsSpan( 6, 3 ).ToArray(), normal ) );
		Assert.NotEqual( 0u, Filter( 2, null, normal ) );
		// The face's own normal is still accepted.
		var face = Vector3.Normalize( Vector3.Cross( B[ 2 ] - A[ 2 ], C[ 2 ] - A[ 2 ] ) );
		Assert.NotEqual( 0u, Filter( 2, codes.AsSpan( 6, 3 ).ToArray(), Words( -face ) ) );
	}

	[Fact]
	public void ARampResting2mmAboveAFloorJoinsItAtItsFoot()
	{
		// Floor quad under everything, and a separate 20-degree ramp face whose foot sits 2.5 mm above it.
		var lift = 0.0025f;
		Vector3[] a = { new( -5, -5, 0 ), new( -5, -5, 0 ), new( 0, -1, lift ), new( 0, -1, lift ) };
		Vector3[] b = { new( 5, -5, 0 ), new( 5, 5, 0 ), Ramp( -1 ) + new Vector3( 0, 0, lift ), Ramp( 1 ) + new Vector3( 0, 0, lift ) };
		Vector3[] c = { new( 5, 5, 0 ), new( -5, 5, 0 ), Ramp( 1 ) + new Vector3( 0, 0, lift ), new( 0, 1, lift ) };
		var codes = TriangleEdgeCodes.Compute( a, b, c, 1e-4f );
		// Ramp triangle 3's edge (C, A) = ((0,1), (0,-1)) is its foot: concave, coded for the 20-degree bend.
		var foot = codes[ 3 * 3 + 2 ];
		Assert.Equal( 0, foot & TriangleEdgeCodes.Convex );
		Assert.True( TriangleEdgeCodes.Cosine( foot & 31 ) <= MathF.Cos( 0.349f ) + 1e-4f );
		// Lifted further than the resting gap, the foot is an open edge again.
		for ( var i = 2; i < 4; ++i )
		{
			a[ i ] += new Vector3( 0, 0, 0.02f );
			b[ i ] += new Vector3( 0, 0, 0.02f );
			c[ i ] += new Vector3( 0, 0, 0.02f );
		}
		Assert.Equal( TriangleEdgeCodes.Convex, TriangleEdgeCodes.Compute( a, b, c, 1e-4f )[ 3 * 3 + 2 ] & 0x3f );
	}

	private static uint Filter( int triangle, byte[] codes, uint[] normal )
	{
		var vertices = new uint[ 12 ];
		Write( A[ triangle ], vertices, 0 );
		Write( B[ triangle ], vertices, 4 );
		Write( C[ triangle ], vertices, 8 );
		var transform = new uint[ 16 ];
		transform[ 0 ] = transform[ 5 ] = transform[ 10 ] = transform[ 15 ] = 0x3f800000u;
		var descriptor = new uint[ 44 ];
		CollisionWorldTriangleConstruction.Construct( vertices, transform, descriptor, 0, 0, 1, codes ?? default );
		return CollisionTriangleFilter.Apply( descriptor, normal, true, new uint[ 136 ],
			new TriangleContactFilterContext( 0x3f7fbe77u, 0x3c23d70au, false ) );
	}

	private static uint[] Words( Vector3 value ) =>
		new[] { BitConverter.SingleToUInt32Bits( value.X ), BitConverter.SingleToUInt32Bits( value.Y ),
			BitConverter.SingleToUInt32Bits( value.Z ), 0u };

	private static void Write( Vector3 value, uint[] words, int offset )
	{
		words[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		words[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		words[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		words[ offset + 3 ] = 0x3f800000u;
	}
}
