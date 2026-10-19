using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ArticulatedBoardMixedContactsTests
{
	[Fact]
	public void GenericContactRetainsOrderedFrictionRequirementAfterGeneratedPrefix()
	{
		var board = new ArticulatedBoard( new SkateSettings(),
			[new( .26f, .095f, -.0665f ), new( .26f, -.095f, -.0665f ),
			 new( -.26f, .095f, -.0665f ), new( -.26f, -.095f, -.0665f )], .0285f );
		var record = new uint[ 64 ];
		record[ 3 ] = 1;
		record[ 7 ] = 8;
		record[ 10 ] = BitConverter.SingleToUInt32Bits( 1f );
		record[ 15 ] = BitConverter.SingleToUInt32Bits( .1f );
		record[ 19 ] = BitConverter.SingleToUInt32Bits( .8f );
		record[ 35 ] = BitConverter.SingleToUInt32Bits( 1f );
		record[ 43 ] = 4;
		record[ 47 ] = 1;
		var generic = new CollisionContact( 0, 7, Vector3.Zero, Vector3.Zero,
			Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, .1f, .8f, 0 );
		Assert.Throws<ArgumentOutOfRangeException>( () => board.StepGenerated( record,
			[new( 1, 0 ), new( 8, ArticulatedBoard.StaticMapBodyIndex )], [generic], 0, 0, .001f ) );
		Assert.Equal( Vector3.Zero, board.Deck.Position );
	}
}
