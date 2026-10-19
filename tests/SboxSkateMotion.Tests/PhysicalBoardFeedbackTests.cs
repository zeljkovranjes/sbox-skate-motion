using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardFeedbackTests
{
	[Fact]
	public void FeedbackUsesSeventhPhysicalPositionAndWritesOnlyCountedBodies()
	{
		var bodies = Bodies();
		bodies[ 6 ].Body.Pose = LimbRigidTransform.Identity with { Translation = new( 1, 2, 3 ) };
		bodies[ 6 ].LocalOffset = LimbRigidTransform.Identity with { Translation = new( 100, 200, 300 ) };
		PhysicalBoardFeedback.Update( bodies, 7, new( 2, 4, 6 ), .5f, 1f );
		for ( var i = 0; i < 7; i++ )
		{
			Assert.Equal( new Vector3( 2, 4, 6 ), bodies[ i ].Body.Velocity );
			Assert.Equal( Vector3.One, bodies[ i ].Body.AngularVelocity );
		}
		Assert.Equal( new Vector3( 9, 8, 7 ), bodies[ 7 ].Body.Velocity );
	}

	[Fact]
	public void ZeroBodyCountLeavesVelocitiesUntouched()
	{
		var bodies = Bodies();
		PhysicalBoardFeedback.Update( bodies, 0, new( 100, 200, 300 ), .1f, 1f );
		foreach ( var body in bodies )
			Assert.Equal( new Vector3( 9, 8, 7 ), body.Body.Velocity );
	}

	[Fact]
	public void SharedReferenceBodyUsesItsPositionBeforeAnyVelocityWrite()
	{
		var bodies = Bodies();
		bodies[ 6 ].Body = bodies[ 0 ].Body;
		bodies[ 0 ].Body.Pose = LimbRigidTransform.Identity with { Translation = new( 2, 3, 4 ) };
		PhysicalBoardFeedback.Update( bodies, 7, new( 4, 6, 8 ), .5f, .5f );
		for ( var i = 0; i < 7; i++ )
			Assert.Equal( new Vector3( 2, 3, 4 ), bodies[ i ].Body.Velocity );
		Assert.Equal( new Vector3( 2, 3, 4 ), bodies[ 0 ].Body.Pose.Translation );
	}

	private static PhysicalBodyBinding[] Bodies()
	{
		var bodies = new PhysicalBodyBinding[ 8 ];
		for ( var i = 0; i < bodies.Length; i++ )
			bodies[ i ] = new() { Body = new() { Pose = LimbRigidTransform.Identity,
				Velocity = new( 9, 8, 7 ), AngularVelocity = Vector3.One } };
		return bodies;
	}
}
