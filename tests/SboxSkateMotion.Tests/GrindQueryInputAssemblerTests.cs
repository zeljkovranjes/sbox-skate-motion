using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindQueryInputAssemblerTests
{
	[Theory]
	[InlineData( 0UL, 1 )]
	[InlineData( 0x1000UL, 1 )]
	[InlineData( 0x2000UL, 7 )]
	[InlineData( 0x4000UL, 1 )]
	[InlineData( 0x200000000000UL, 1 )]
	[InlineData( ulong.MaxValue, 7 )]
	public void DirectionCountUsesOnlyTheRequiredMotionFlag( ulong flags, int count )
	{
		var board = new GrindQueryBoardState { MotionFlags = flags };
		var input = new GrindQueryRequestInput { Count = 123 };
		Assert.Equal( count, GrindQueryInputAssembler.Assemble( input, board, 0, 0 ).Count );
	}

	[Fact]
	public void PoseAssemblyRetainsAllVectorLanesAndTheLocalTranslation()
	{
		var frame = new Matrix4x4( 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 );
		var local = Matrix4x4.CreateTranslation( 19, 23, 29 );
		var board = new GrindQueryBoardState( frame, local, new( 2, 3, 5, 7 ), new( 11, 13, 17, 19 ),
			new( 23, 29, 31, 37 ), new( 41, 43, 47, 53 ), -.25f, 0 );
		var result = GrindQueryInputAssembler.Assemble( default, board, 30, .75f );
		Assert.Equal( new Vector4( 5, 6, 7, 8 ), result.Up );
		Assert.Equal( local, result.LocalFrame );
		Assert.Equal( board.Velocity, result.Velocity );
		Assert.Equal( board.SecondaryVelocity, result.SecondaryVelocity );
		Assert.Equal( board.Reference, result.Reference );
		Assert.Equal( board.Point, result.Point );
		Assert.Equal( -.25f, result.LeadTime );
		Assert.Equal( 30, result.Spread );
		Assert.Equal( .75f, result.SpreadScale );
	}

	[Fact]
	public void CallerOverridesAndExternalQueryContextSurviveAssembly()
	{
		var input = new GrindQueryRequestInput
		{
			SecondarySpeed = true, Override = true, AllowReuse = true,
			OverridePosition = new( 1, 2, 3, 4 ), OverrideReference = new( 5, 6, 7, 8 ),
			ExternalReference = new( 9, 10, 11, 12 ), ExternalUp = new( 13, 14, 15, 16 ),
			ExternalVelocity = new( 17, 18, 19, 20 ), RetainedAxis = new( 21, 22, 23, 24 ),
			RetainedTrajectory = new( new( 25 ), new( 26 ), new( 27 ), new( 28 ) ),
			Acceleration = new( 29, 30, 31, 32 ), Queue = 3, Owner = 5
		};
		var result = GrindQueryInputAssembler.Assemble( input, default, 0, 0 );
		// Only the count differs when all board-derived fields start at their defaults.
		Assert.Equal( input with { Count = 1 }, result );
	}
}
