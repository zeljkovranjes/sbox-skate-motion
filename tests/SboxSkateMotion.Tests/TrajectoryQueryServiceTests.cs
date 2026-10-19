using System;
using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrajectoryQueryServiceTests
{
	private static TrajectoryQueryRequest Request => new( new( -1, 1, -1 ), new( 0, -1, 0 ), new( 0, -9.81f, 0 ),
		2, .1f, .25f, 1.2f, -1, 0, new( 0, 0, 0, 0, true, true ) );
	private static TrajectoryWorldQuery Query() => new( new( 1e-8f, -.00001f, 2.938735877055719e-39f,
		1.52587890625e-5f, 1e-6f ), new( 1f / 60f, .0001f, .7f, 1e-6f ) );
	private static MeshQueryInstance[] Floor()
	{
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return [new( new( [new( new( -5, 0, -5 ), new( -5, 0, 5 ), new( 5, 0, -5 ), 3 )],
			new( -5 ), new( 5 ) ), identity, identity, new( -5 ), new( 5 ), -1, 0, 123 )];
	}

	[Theory]
	[InlineData( 0, 0, 1 )]
	[InlineData( 1, 1, 0 )]
	[InlineData( 2, 0, 1 )]
	[InlineData( -1, 0, 1 )]
	public void OnlyOneSelectsTheFirstQueue( int queue, int first, int second )
	{
		var service = new TrajectoryQueryService( 8, 8 );
		service.Submit( new(), Request, queue, 0 );
		Assert.Equal( first, service.FirstCount );
		Assert.Equal( second, service.SecondCount );
	}

	[Fact]
	public void ACompletedMissHasInvalidContactButIsNotPending()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var target = new TrajectoryQueryTarget();
		service.Submit( target, Request, 1, 255 );
		Assert.False( target.Completed );
		service.Process( Query(), [] );
		Assert.True( target.Completed );
		Assert.Equal( -1f, target.Contact.Time );
		Assert.Equal( -1, target.Contact.Sample );
		Assert.Equal( (byte)255, target.Flag );
		Assert.Equal( 0, service.FirstCount );
	}

	[Fact]
	public void ResubmissionClearsThePreviousHitBeforeExecution()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var target = new TrajectoryQueryTarget();
		service.Submit( target, Request, 1, 1 );
		service.Process( Query(), Floor() );
		Assert.True( target.Contact.Time >= 0f );
		service.Submit( target, Request with { Duration = 0 }, 0, 2 );
		Assert.False( target.Completed );
		Assert.Equal( TrajectoryContact.Empty, target.Contact );
		service.Process( Query(), Floor() );
		Assert.True( target.Completed );
		Assert.Equal( TrajectoryContact.Empty, target.Contact );
		Assert.Equal( (byte)2, target.Flag );
	}

	[Fact]
	public void CancellationAppliesToBothQueuesAndAnonymousOwnerUsesSlotSeven()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var first = new TrajectoryQueryTarget();
		var second = new TrajectoryQueryTarget();
		service.Submit( first, Request, 1, 0 );
		service.Submit( second, Request with { Filter = 7 }, 0, 0 );
		service.Cancel( 7 );
		service.Process( Query(), Floor() );
		Assert.False( first.Completed );
		Assert.False( second.Completed );
		Assert.Equal( TrajectoryContact.Empty, first.Contact );
		Assert.Equal( 0, service.FirstCount + service.SecondCount );
	}

	[Fact]
	public void ANewRequestReactivatesAllPendingRequestsForThatOwner()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var first = new TrajectoryQueryTarget();
		var second = new TrajectoryQueryTarget();
		service.Submit( first, Request with { Filter = 2 }, 1, 0 );
		service.Cancel( 2 );
		service.Submit( second, Request with { Filter = 2 }, 0, 0 );
		service.Process( Query(), Floor() );
		Assert.True( first.Completed );
		Assert.True( second.Completed );
	}

	[Fact]
	public void QueueDeliveryKeepsTheLatestRequestWhenTargetsAreShared()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var target = new TrajectoryQueryTarget();
		service.Submit( target, Request with { Position = new( -1, 2, -1 ) }, 0, 1 );
		service.Submit( target, Request, 1, 2 );
		service.Process( Query(), Floor() );
		var expected = Query().Evaluate( Request with { Position = new( -1, 2, -1 ) }, Floor() );
		Assert.Equal( expected, target.Contact );
		Assert.Equal( Request, target.Request );
		Assert.Equal( (byte)2, target.Flag );
	}

	[Fact]
	public void ServiceSubmissionClearsOptionalSurfaceMasks()
	{
		var service = new TrajectoryQueryService( 8, 8 );
		var target = new TrajectoryQueryTarget();
		service.Submit( target, Request with { Surfaces = new( 8, 0, 0, 0, false, false ) }, 1, 0 );
		service.Process( Query(), Floor() );
		Assert.True( target.Contact.Time >= 0f );
		Assert.Equal( Request.Surfaces, target.Request.Surfaces );
	}

	[Fact]
	public void CapacityFailureDoesNotResetAnExistingTarget()
	{
		var service = new TrajectoryQueryService( 1, 1 );
		var target = new TrajectoryQueryTarget();
		service.Submit( target, Request, 1, 9 );
		Assert.Throws<InvalidOperationException>( () => service.Submit( target, Request with { Duration = 0 }, 1, 0 ) );
		Assert.Equal( Request, target.Request );
		Assert.Equal( (byte)9, target.Flag );
	}
}
