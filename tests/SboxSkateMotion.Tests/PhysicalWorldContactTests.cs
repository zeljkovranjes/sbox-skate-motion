using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalWorldContactTests
{
	[Fact]
	public void OppositeSidesKeepTheirBonesSurfacesAndOtherBodySelection()
	{
		var collector = new PhysicalWorldContactCollector();
		var a = new PhysicalBodyState { Velocity = new( 1f, 2f, 3f ), InverseMass = .5f };
		var b = new PhysicalBodyState { Velocity = new( -2f, 3f, 1f ), MotionMode = 1 };
		var first = new PhysicalContactOwner { Kind = 4 };
		var second = new PhysicalContactOwner { Kind = 5, Identifier = 27 };
		collector.Register( a, first, 7 );
		collector.Register( b, second, 19 );
		collector.Collect( [Contact( a, b )] );
		var left = first.Contact( 0 );
		var right = second.Contact( 0 );
		Assert.Equal( new Vector3( 3f, -1f, 2f ), left.RelativeVelocity );
		Assert.Equal( -left.RelativeVelocity, right.RelativeVelocity );
		Assert.Equal( Vector3.UnitY, left.RiderContact.Normal );
		Assert.Equal( -Vector3.UnitY, right.RiderContact.Normal );
		Assert.Equal( 0x1234, left.RiderContact.SurfaceFlags );
		Assert.Equal( 0xabcd, right.RiderContact.SurfaceFlags );
		Assert.Equal( 7, left.RiderContact.Bone );
		Assert.Equal( 19, right.RiderContact.Bone );
		Assert.True( left.RiderContact.SelectSecondBody );
		Assert.False( right.RiderContact.SelectSecondBody );
		Assert.True( left.RiderContact.SecondBody.Fixed );
		Assert.Equal( 5, left.RiderContact.OtherKind );
		Assert.Equal( 27, left.RiderContact.OtherIdentifier );
		Assert.Same( second, left.OtherOwner );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SelfAndSharedActorContactsAreSuppressed( bool sharedActor )
	{
		var collector = new PhysicalWorldContactCollector();
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var first = new PhysicalContactOwner { ActorIdentity = new object() };
		var second = sharedActor ? new PhysicalContactOwner { ActorIdentity = first.ActorIdentity } : first;
		collector.Register( a, first, 1 );
		collector.Register( b, second, 2 );
		collector.Collect( [Contact( a, b )] );
		Assert.Equal( 0, first.Count );
		Assert.Equal( 0, second.Count );
	}

	[Fact]
	public void FullFirstOwnerDoesNotPreventReportingToSecondOwner()
	{
		var collector = new PhysicalWorldContactCollector();
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var unregistered = new PhysicalBodyState();
		var first = new PhysicalContactOwner();
		var second = new PhysicalContactOwner();
		collector.Register( a, first, 1 );
		collector.Register( b, second, 2 );
		var contacts = new PhysicalWorldContact[ 18 ];
		for ( var i = 0; i < 17; ++i )
			contacts[ i ] = Contact( a, unregistered );
		contacts[ 17 ] = Contact( a, b );
		collector.Collect( contacts );
		Assert.Equal( 16, first.Count );
		Assert.Equal( 15, first.Contact( 15 ).SourceIndex );
		Assert.Null( first.Contact( 0 ).OtherOwner );
		Assert.Equal( 1, second.Count );
		Assert.Equal( 17, second.Contact( 0 ).SourceIndex );
	}

	[Fact]
	public void UnregisterAndReassignClearPreviouslyRetainedOwnerLists()
	{
		var collector = new PhysicalWorldContactCollector();
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var first = new PhysicalContactOwner();
		var replacement = new PhysicalContactOwner();
		collector.Register( a, first, 1 );
		collector.Collect( [Contact( a, b )] );
		Assert.Equal( 1, first.Count );
		collector.Register( a, replacement, 8 );
		collector.Collect( [Contact( a, b )] );
		Assert.Equal( 0, first.Count );
		Assert.Equal( 8, replacement.Contact( 0 ).RiderContact.Bone );
		collector.Unregister( a );
		collector.Collect( [Contact( a, b )] );
		Assert.Equal( 0, replacement.Count );
		Assert.Empty( replacement.RiderContacts.ToArray() );
		Assert.Throws<ArgumentOutOfRangeException>( () => replacement.Contact( 0 ) );
	}

	[Fact]
	public void EmptyUpdateRemovesEveryPreviouslyReportedContact()
	{
		var collector = new PhysicalWorldContactCollector();
		var a = new PhysicalBodyState();
		var b = new PhysicalBodyState();
		var owner = new PhysicalContactOwner();
		collector.Register( a, owner, 1 );
		collector.Collect( [Contact( a, b )] );
		collector.Collect( [] );
		Assert.Equal( 0, owner.Count );
		Assert.Empty( owner.RiderContacts.ToArray() );
	}

	private static PhysicalWorldContact Contact( PhysicalBodyState a, PhysicalBodyState b ) =>
		new( a, b, Vector4.UnitY, new( 1f, 2f, 3f, 1f ), new( 4f, 5f, 6f ), 0xabcd1234 );
}
