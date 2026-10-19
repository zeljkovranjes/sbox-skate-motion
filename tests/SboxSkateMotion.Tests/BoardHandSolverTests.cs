using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandSolverTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void HeldDriveUsesTheHandFrameFirstAndTheDeckFrameSecond( int hand )
	{
		var constraints = new BoardHeldConstraints();
		var board = Pose( new( 2f, -1f, 3f ), Quaternion.CreateFromAxisAngle( Vector3.UnitZ, .6f ) );
		var attachment = Pose( new( 3f, 2f, 4f ), Quaternion.CreateFromAxisAngle( Vector3.UnitY, -.4f ) );
		constraints.Activate( hand, board, attachment );
		var geometry = constraints.Geometry( hand, 32 + hand, 0 );
		Assert.Equal( 32 + hand, geometry.BodyA );
		Assert.Equal( 0, geometry.BodyB );
		Assert.Equal( Vector3.Zero, geometry.LocalAnchorA );
		Assert.Equal( Quaternion.Identity, geometry.LocalFrameA );
		Assert.InRange( Vector3.Distance( board.Transform( geometry.LocalAnchorB ), attachment.Translation ), 0f, .00001f );
		var boardRotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, .6f );
		var handRotation = Quaternion.CreateFromAxisAngle( Vector3.UnitY, -.4f );
		Assert.InRange( MathF.Abs( Quaternion.Dot( boardRotation * geometry.LocalFrameB, handRotation ) ), .99999f, 1.00001f );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void EachHandCanMoveTheBoardAndReleaseLeavesItFree( int hand )
	{
		var board = CreateBoard();
		var anchor = new Vector3( .15f, -.1f, .25f );
		var pose = Pose( anchor, Quaternion.Identity );
		board.HeldConstraints.Activate( hand, LimbRigidTransform.Identity, pose );
		var moved = anchor + new Vector3( .05f, .02f, .06f );
		board.SetHandBody( hand, new DynamicBody { Position = moved, Rotation = Quaternion.Identity } );
		for ( var i = 0; i < 120; ++i )
		{
			board.BeginStep();
			board.Step( [], 0f, 0f, 1f / 240f );
		}
		var actual = board.Deck.Position + Vector3.Transform( anchor, board.Deck.Rotation );
		Assert.InRange( Vector3.Distance( actual, moved ), 0f, .002f );
		Assert.Equal( moved, board.HandBody( hand ).Position );
		Assert.InRange( board.MaximumAnchorError(), 0f, .002f );
		board.HeldConstraints.Release();
		board.SetHandBody( hand, new DynamicBody { Position = new( 100f, 200f, 300f ), Rotation = Quaternion.Identity } );
		var velocity = board.Deck.Velocity;
		board.BeginStep();
		board.Step( [], 0f, 0f, 1f / 240f );
		Assert.InRange( Vector3.Distance( board.Deck.Velocity, velocity - Vector3.UnitZ * (9.81f / 240f) ), 0f, .002f );
	}

	[Fact]
	public void DynamicHandReceivesTheOppositeReactionForItsCharacterOwner()
	{
		var board = CreateBoard();
		board.HeldConstraints.Activate( 0, LimbRigidTransform.Identity, LimbRigidTransform.Identity );
		var hand = new DynamicBody
		{
			Position = new( .01f, 0f, 0f ), Rotation = Quaternion.Identity,
			InverseMass = .25f, WorldInverseInertia = Matrix4x4.Identity
		};
		board.SetHandBody( 0, hand );
		board.BeginStep();
		board.Step( [], 0f, 0f, 1f / 240f );
		Assert.True( board.Deck.Velocity.X > 0f );
		Assert.True( board.HandResponse( 0 ).LinearVelocity.X < 0f );
		Assert.Equal( hand.Position, board.HandBody( 0 ).Position );
		Assert.Equal( hand.Velocity, board.HandBody( 0 ).Velocity );
		var momentum = Vector3.Zero;
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
			momentum += board.Body( i ).Velocity / board.Body( i ).InverseMass;
		momentum += board.HandResponse( 0 ).LinearVelocity * 240f / hand.InverseMass;
		Assert.InRange( MathF.Abs( momentum.X ), 0f, .0001f );
	}

	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 1 )]
	[InlineData( 0, 2 )]
	public void HeldPoseAndHandDrivesRotateTheDeckAboutItsAnchor( int hand, int axis )
	{
		var board = CreateBoard();
		var anchor = new Vector3( .15f, -.1f, .25f );
		board.HeldConstraints.Activate( hand, LimbRigidTransform.Identity, Pose( anchor, Quaternion.Identity ) );
		var direction = axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;
		var rotation = Quaternion.CreateFromAxisAngle( direction, .8f );
		board.SetHandBody( hand, new DynamicBody { Position = anchor, Rotation = rotation } );
		board.SetTargetFrame( anchor - Vector3.Transform( anchor, rotation ), rotation );
		board.TargetDrive.FollowPose( 1f );
		for ( var i = 0; i < 120; ++i )
		{
			board.BeginStep();
			board.Step( [], 0f, 0f, 1f / 240f );
		}
		Assert.InRange( MathF.Abs( Quaternion.Dot( board.Deck.Rotation, rotation ) ), .99999f, 1.00001f );
		Assert.InRange( Vector3.Distance( board.Deck.Position + Vector3.Transform( anchor, board.Deck.Rotation ), anchor ), 0f, .002f );
	}

	[Fact]
	public void MissingHandBodyCannotSilentlyBecomeAnImmovableTarget()
	{
		var board = CreateBoard();
		board.HeldConstraints.Activate( 0, LimbRigidTransform.Identity, LimbRigidTransform.Identity );
		Assert.Throws<InvalidOperationException>( () => board.Step( [], 0f, 0f, 1f / 240f ) );
	}

	[Fact]
	public void HandStorageDoesNotAliasContactSlotsOrTheSeparateTarget()
	{
		var board = CreateBoard();
		board.SetTargetFrame( new( 10f, 20f, 30f ), Quaternion.Identity );
		board.SetHandBody( 0, new DynamicBody { Position = new( 1f, 2f, 3f ), Rotation = Quaternion.Identity } );
		board.SetHandBody( 1, new DynamicBody { Position = new( 4f, 5f, 6f ), Rotation = Quaternion.Identity } );
		for ( var i = 0; i < ArticulatedBoard.ContactCapacity; ++i )
			board.Body( ArticulatedBoard.DynamicCount + i ).Position = new( -i, -i, -i );
		Assert.Equal( new Vector3( 10f, 20f, 30f ), board.TargetBody.Position );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), board.HandBody( 0 ).Position );
		Assert.Equal( new Vector3( 4f, 5f, 6f ), board.HandBody( 1 ).Position );
		Assert.Throws<ArgumentOutOfRangeException>( () => board.SetHandBody( 2, default ) );
	}

	private static LimbRigidTransform Pose( Vector3 position, Quaternion rotation ) => new(
		new( Vector3.Transform( Vector3.UnitX, rotation ), Vector3.Transform( Vector3.UnitY, rotation ),
			Vector3.Transform( Vector3.UnitZ, rotation ) ), position );

	private static ArticulatedBoard CreateBoard()
	{
		var board = new ArticulatedBoard( new SkateSettings(), new Vector3[]
		{
			new( .26f, .095f, -.04f ), new( .26f, -.095f, -.04f ),
			new( -.26f, .095f, -.04f ), new( -.26f, -.095f, -.04f )
		}, .0285f );
		board.SetRiderAttached( false );
		return board;
	}
}
