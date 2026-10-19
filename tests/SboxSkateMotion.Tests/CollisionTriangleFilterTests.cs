using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionTriangleFilterTests
{
	[Theory]
	[InlineData( 0xf0u, true, 0xbf56321cu, 0xbf076776u, 0xbe116e8eu, 64u )]
	[InlineData( 0x80u, false, 0xbea085b4u, 0x3b6cc753u, 0xbf731754u, 128u )]
	public void EdgeAcceptanceRetainsItsFlagMask( uint flags, bool reverse, uint x, uint y, uint z, uint expected )
	{
		var triangle = Triangle();
		triangle[ 37 ] = flags;
		var normal = new uint[] { x, y, z, 0 };
		var contacts = new uint[ 136 ];
		Assert.Equal( expected, CollisionTriangleFilter.Apply( triangle, normal, reverse, contacts, default ) );
	}

	[Theory]
	[MemberData( nameof( CorrectionCases ) )]
	public void BoundaryCorrectionProjectsOnlyTheSelectedContactSide( uint flags, uint[] cosines,
		bool reverse, uint[] inputNormal, uint[] first, uint[] second,
		uint[] expectedNormal, uint[] expectedFirst, uint[] expectedSecond )
	{
		var triangle = Triangle();
		triangle[ 37 ] = flags;
		cosines.CopyTo( triangle, 38 );
		var beforeTriangle = triangle.AsSpan().ToArray();
		var normal = inputNormal.Concat( new uint[] { 0x12345678u } ).ToArray();
		var contacts = Enumerable.Repeat( 0x12345678u, 140 ).ToArray();
		first.CopyTo( contacts, 0 );
		second.CopyTo( contacts, 64 );
		contacts[ 132 ] = 1;
		var beforeContacts = contacts.AsSpan().ToArray();
		Assert.Equal( 1u, CollisionTriangleFilter.Apply( triangle, normal, reverse, contacts,
			new TriangleContactFilterContext( 0x3f800000u, 0x3c23d70au, false ) ) );
		Assert.Equal( expectedNormal, normal.Take( 4 ) );
		Assert.Equal( expectedFirst, contacts.Take( 4 ) );
		Assert.Equal( expectedSecond, contacts.Skip( 64 ).Take( 4 ) );
		for ( var i = 0; i < contacts.Length; ++i )
		{
			if ( i < 4 || i is >= 64 and < 68 )
				continue;
			Assert.Equal( beforeContacts[ i ], contacts[ i ] );
		}
		Assert.Equal( 0x12345678u, normal[ 4 ] );
		Assert.Equal( beforeTriangle, triangle );
	}

	public static IEnumerable<object[]> CorrectionCases()
	{
		yield return new object[]
		{
			0x110u,
			new uint[] { 0x3f4ccccdu, 0x3f4ccccdu, 0x3f4ccccdu },
			false,
			new uint[] { 0x00000000u, 0xbe9fdf38u, 0x3f733333u, 0x00000000u },
			new uint[] { 0x4018dc57u, 0xbfb4f841u, 0xc000cd1fu, 0x00000000u },
			new uint[] { 0x4012b8dau, 0xbf006068u, 0xc01f7184u, 0x00000000u },
			new uint[] { 0x00000000u, 0x00000000u, 0x3f800000u, 0x00000000u },
			new uint[] { 0x4012b8dau, 0xbf006068u, 0xc000cd1fu, 0x00000000u },
			new uint[] { 0x4012b8dau, 0xbf006068u, 0xc01f7184u, 0x00000000u }
		};
		yield return new object[]
		{
			0x390u,
			new uint[] { 0x3ecccccdu, 0x3f4ccccdu, 0x3f7851ecu },
			false,
			new uint[] { 0xbf4b0c44u, 0xbdd5daa7u, 0x3f19999au, 0x00000000u },
			new uint[] { 0x3fe98661u, 0x3fa81739u, 0xbf7a78d4u, 0x00000000u },
			new uint[] { 0x402df72du, 0xbeae2b71u, 0xc025ca40u, 0x00000000u },
			new uint[] { 0xbf4c29fdu, 0x00000000u, 0x3f1a71beu, 0x00000000u },
			new uint[] { 0x3fafe286u, 0xbeae2b71u, 0xbfc96e97u, 0x00000000u },
			new uint[] { 0x402df72du, 0xbeae2b71u, 0xc025ca40u, 0x00000000u }
		};
		yield return new object[]
		{
			0x110u,
			new uint[] { 0x3f4ccccdu, 0x3f4ccccdu, 0x3f4ccccdu },
			true,
			new uint[] { 0x80000000u, 0x3e9fdf38u, 0xbf733333u, 0x00000000u },
			new uint[] { 0xbf7d71fau, 0x40263b8eu, 0x3fccb384u, 0x00000000u },
			new uint[] { 0xbfa09c85u, 0x3f9b2de3u, 0x403a4850u, 0x00000000u },
			new uint[] { 0x80000000u, 0x80000000u, 0xbf800000u, 0x80000000u },
			new uint[] { 0xbf7d71fau, 0x40263b8eu, 0x3fccb384u, 0x00000000u },
			new uint[] { 0xbf7d71fau, 0x40263b8eu, 0x403a4850u, 0x00000000u }
		};
		yield return new object[]
		{
			0x390u,
			new uint[] { 0x3ecccccdu, 0x3f4ccccdu, 0x3f7851ecu },
			true,
			new uint[] { 0x3f4b0c44u, 0x3dd5daa7u, 0xbf19999au, 0x00000000u },
			new uint[] { 0xbe7ad18fu, 0xc036524fu, 0x400968d5u, 0x00000000u },
			new uint[] { 0x3fddc5c4u, 0x4012264au, 0xc01c9f9bu, 0x00000000u },
			new uint[] { 0x3f4c29fdu, 0x80000000u, 0xbf1a71beu, 0x80000000u },
			new uint[] { 0xbe7ad18fu, 0xc036524fu, 0x400968d5u, 0x00000000u },
			new uint[] { 0x404e4adau, 0xc036524fu, 0xbef40708u, 0x00000000u }
		};
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	[InlineData( 4 )]
	public void InvalidArgumentsFailBeforeChangingCallerStorage( int scenario )
	{
		var triangle = Triangle();
		if ( scenario == 0 )
			triangle = triangle.Take( 43 ).ToArray();
		if ( scenario == 1 )
			triangle[ 36 ] = 5;
		var normal = Enumerable.Repeat( 0x12345678u, scenario == 2 ? 3 : 4 ).ToArray();
		var contacts = Enumerable.Repeat( 0x12345678u, scenario == 3 ? 135 : 136 ).ToArray();
		contacts[ 132 ] = scenario == 4 ? 17u : 0u;
		var before = contacts.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionTriangleFilter.Apply( triangle, normal, false, contacts, default ) );
		Assert.All( normal, word => Assert.Equal( 0x12345678u, word ) );
		Assert.Equal( before, contacts );
	}

	[Fact]
	public void RejectedEdgePreservesNormalAndContactPairs()
	{
		var triangle = Triangle();
		var normal = new uint[] { 0xbf56321cu, 0xbf076776u, 0xbe116e8eu, 0 };
		var beforeNormal = normal.AsSpan().ToArray();
		var contacts = Enumerable.Repeat( 0x12345678u, 136 ).ToArray();
		contacts[ 132 ] = 16;
		var beforeContacts = contacts.AsSpan().ToArray();
		Assert.Equal( 0u, CollisionTriangleFilter.Apply( triangle, normal, true, contacts, default ) );
		Assert.Equal( beforeNormal, normal );
		Assert.Equal( beforeContacts, contacts );
	}

	private static uint[] Triangle()
	{
		var source = new uint[ 24 ];
		source[ 3 ] = source[ 7 ] = source[ 11 ] = 0x3f800000u;
		source[ 4 ] = 0x40000000u;
		source[ 9 ] = 0x40400000u;
		source[ 14 ] = 0x3f800000u;
		var triangle = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, triangle, ReadOnlySpan<uint>.Empty, 0 ) );
		return triangle;
	}
}
