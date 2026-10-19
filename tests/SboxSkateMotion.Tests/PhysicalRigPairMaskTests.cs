using System.Text.Json;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigPairMaskTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 18 ).Select( index => new object[] { index } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void MatchesEveryCapturedPairAndPreservesPadding( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigPairMaskFixtures.Json );
		var sample = document.RootElement.GetProperty( "cases" )[ index ];
		var mask = new PhysicalRigPairMask( sample.GetProperty( "kind" ).GetInt32() != 0,
			sample.GetProperty( "mode" ).GetInt32() != 0 );
		var words = Enumerable.Repeat( sample.GetProperty( "initial" ).GetUInt32(), 22 ).ToArray();
		mask.Write( words );
		Assert.Equal( sample.GetProperty( "words" ).EnumerateArray().Select( x => x.GetUInt32() ).ToArray(), words );
		var bits = sample.GetProperty( "bits" );
		for ( var first = 0; first < 26; ++first )
			for ( var second = 0; second < 26; ++second )
				Assert.Equal( bits[ first * 26 + second ].GetInt32() != 0, mask.Excludes( first, second ) );
	}

	[Fact]
	public void RejectsNonCanonicalIndicesAndIncorrectStorage()
	{
		var mask = new PhysicalRigPairMask( false, false );
		Assert.Throws<ArgumentOutOfRangeException>( () => mask.Excludes( -1, 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => mask.Excludes( 0, 26 ) );
		Assert.Throws<ArgumentException>( () => mask.Write( new uint[ 21 ] ) );
	}
}
