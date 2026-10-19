using SkateMotion.Core.Animation;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace SboxSkateMotion.Tests;

public sealed class PhysicalBoardInputPublisherTests
{
	[Fact]
	public void CachedSeventhBodyPublishesThePhysicalFrameAndStanceInverse()
	{
		var bodies = Bodies();
		bodies[ 6 ].CachedPose = new( LimbRigidTransform.Identity.Rotation, new( 2f, 3f, 4f ) );
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, bodies, 0, 0, 2f, Vector3.UnitZ );
		Assert.Equal( bodies[ 6 ].CachedPose, state.PhysicalFrame );
		Assert.Equal( new Vector3( -2f, -3f, -4f ), state.InverseStanceFrame.Translation );
		Assert.Equal( Vector3.UnitZ, state.HorizontalDirection );
		Assert.Equal( Vector3.UnitY, state.PerpendicularDirection );
		Assert.Equal( 0f, state.Mass );
	}

	[Fact]
	public void PhysicalBodyAndOffsetReplaceTheCachedFrame()
	{
		var bodies = Bodies();
		bodies[ 6 ].Body = new() { Pose = new( LimbRigidTransform.Identity.Rotation, new( 1f, 2f, 3f ) ) };
		bodies[ 6 ].LocalOffset = new( LimbRigidTransform.Identity.Rotation, new( .25f, .5f, 1f ) );
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, bodies, 0, 0, 1f, Vector3.UnitZ );
		Assert.Equal( new Vector3( 1.25f, 2.5f, 4f ), state.PhysicalFrame.Translation );
		Assert.Equal( LimbRigidTransform.Identity, bodies[ 6 ].CachedPose );
	}

	[Fact]
	public void MirroredStanceKeepsThePhysicalFrameAndReversesTwoAxes()
	{
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, Bodies(), 0, 1UL << 20, -2f, Vector3.UnitZ );
		Assert.Equal( LimbRigidTransform.Identity, state.PhysicalFrame );
		Assert.Equal( -Vector3.UnitX, state.StanceFrame.Rotation.X );
		Assert.Equal( Vector3.UnitY, state.StanceFrame.Rotation.Y );
		Assert.Equal( -Vector3.UnitZ, state.StanceFrame.Rotation.Z );
		Assert.Equal( -1f, state.StanceSign );
		Assert.Equal( Vector3.UnitZ * 2f, state.SignedVelocityDirection );
		Assert.Equal( Vector3.UnitZ, state.TravelDirection );
		Assert.Equal( 2f, state.Speed );
	}

	[Fact]
	public void MassAccumulatesReciprocalsOfExactlyTheCountedBodies()
	{
		var bodies = Bodies();
		bodies[ 0 ].Body = new() { InverseMass = .5f };
		bodies[ 1 ].Body = new() { InverseMass = .25f };
		bodies[ 2 ].Body = new() { InverseMass = 2f };
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, bodies, 3, 0, 0f, Vector3.UnitZ );
		Assert.Equal( 6.5f, state.Mass );
	}

	[Fact]
	public void ZeroSpeedChoosesNegativeTravelAndRetainsNegativeZeroMagnitude()
	{
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, Bodies(), 0, 0, 0f, Vector3.UnitZ );
		Assert.Equal( -Vector3.UnitZ, state.TravelDirection );
		Assert.Equal( int.MinValue, BitConverter.SingleToInt32Bits( state.Speed ) );
	}

	[Theory]
	[InlineData( .003f, .003f )]
	[InlineData( .004f, 1f )]
	public void HorizontalAxisNormalizationUsesTheSquaredThreshold( float component, float expected )
	{
		var bodies = Bodies();
		bodies[ 6 ].CachedPose = new( new( Vector3.UnitX, Vector3.UnitY, new( 0f, 0f, component ) ), Vector3.Zero );
		var state = new PhysicalBoardInputState();
		PhysicalBoardInputPublisher.Update( state, bodies, 0, 0, 1f, Vector3.UnitZ );
		Assert.Equal( expected, state.HorizontalDirection.Z );
	}

	[Fact]
	public void AlignedHeadingCopiesTheTargetWithoutNormalizingIt()
	{
		var state = new PhysicalBoardInputState { Heading = Vector3.UnitZ };
		PhysicalBoardInputPublisher.Update( state, Bodies(), 0, 0, 1f, Vector3.UnitZ * 2f );
		Assert.Equal( Vector3.UnitZ * 2f, state.Heading );
	}

	[Fact]
	public void HeadingUsesRetainedStateAndSuppressesNearZeroBlends()
	{
		var state = new PhysicalBoardInputState { Heading = new( -.1f, 0f, 0f ) };
		var bodies = Bodies();
		PhysicalBoardInputPublisher.Update( state, bodies, 0, 0, 1f, new( .9f, 0f, 0f ) );
		Assert.Equal( Vector3.Zero, state.Heading );
		PhysicalBoardInputPublisher.Update( state, bodies, 0, 0, 1f, Vector3.UnitZ );
		Assert.Equal( Vector3.UnitZ, state.Heading );
	}

	[Fact]
	public void InvalidMassBindingRejectsBeforePublishingPartialState()
	{
		var state = new PhysicalBoardInputState { Mass = 31f, Heading = Vector3.UnitX };
		Assert.Throws<ArgumentException>( () => PhysicalBoardInputPublisher.Update( state, Bodies(), 1, 0, 1f, Vector3.UnitZ ) );
		Assert.Equal( 31f, state.Mass );
		Assert.Equal( Vector3.UnitX, state.Heading );
	}

	private static PhysicalBodyBinding[] Bodies()
	{
		var bodies = new PhysicalBodyBinding[ 7 ];
		for ( var i = 0; i < bodies.Length; i++ )
			bodies[ i ] = new() { CachedPose = LimbRigidTransform.Identity };
		return bodies;
	}
}
