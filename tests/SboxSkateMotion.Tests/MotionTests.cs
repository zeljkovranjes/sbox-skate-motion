using System.Numerics;
using SkateMotion.Core;
using Xunit;

namespace SboxSkateMotion.Tests;

public class MotionTests
{
	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	[InlineData( 240 )]
	public void FullPushCyclesDeliverTheSameImpulseAtDifferentRates( int rate )
	{
		var settings = new SkateSettings { PushPeriod = 0.5f };
		var movement = new SkateMovement();
		var sum = 0f;
		for ( var i = 0; i < rate * 2; i++ )
			sum += movement.PushAcceleration( new( Push: 1f ), 0f, settings, 1f / rate ) / rate;
		Assert.InRange( sum, 9.9999f, 10.0001f );
	}

	[Fact]
	public void SpeedLimitStopsPushingWithoutClampingDownhillMomentum()
	{
		var settings = new SkateSettings();
		var movement = new SkateMovement();
		Assert.Equal( 0f, movement.PushAcceleration( new( Push: 1f ), 12f, settings, 1f / 60f ) );
		Assert.Equal( 0f, movement.PushAcceleration( new( Push: 1f, Brake: 1f ), 0f, settings, 1f / 60f ) );
	}

	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void FlatDropSettlesWithoutEnergyGrowth( int rate )
	{
		var settings = new SkateSettings();
		var physics = new SkatePhysics( settings );
		var contacts = new WheelContact[ 4 ];
		var forces = new WheelForce[ 4 ];
		var position = new Vector3( 0f, 0f, 0.35f );
		var velocity = Vector3.Zero;
		for ( var frame = 0; frame < rate * 8; frame++ )
		{
			var body = new BoardBody( position, Quaternion.Identity, velocity, Vector3.Zero, new( 3f, 5f, 4f ) );
			for ( var i = 0; i < 4; i++ )
				contacts[ i ] = new( true, SkateWheels.Offset( i, settings ), Vector3.UnitZ, Vector3.Zero, position.Z );
			var result = physics.Step( body, contacts, default, 1f / rate, forces );
			var force = result.Force;
			foreach ( var wheel in forces )
				force += wheel.Force;
			velocity += force / settings.Mass / rate;
			position += velocity / rate;
			Assert.True( float.IsFinite( position.Z ) );
			Assert.InRange( position.Z, 0.01f, 0.36f );
		}
		Assert.InRange( MathF.Abs( velocity.Z ), 0f, 0.001f );
		Assert.InRange( position.Z, 0.05f, settings.RideHeight );
	}

	[Fact]
	public void LedgeLosesOnlyUnsupportedWheels()
	{
		var contacts = Contacts();
		contacts[ 0 ] = default;
		contacts[ 1 ] = default;
		var forces = new WheelForce[ 4 ];
		var result = new SkatePhysics( new() ).Step( Body(), contacts, default, 1f / 60f, forces );
		Assert.Equal( 2, result.WheelCount );
		Assert.Equal( 0f, forces[ 0 ].Load );
		Assert.Equal( 0f, forces[ 1 ].Load );
		Assert.True( forces[ 2 ].Load > 0f && forces[ 3 ].Load > 0f );
	}

	[Fact]
	public void OllieProducesOneImpulseAndReleasesGroundContacts()
	{
		var physics = new SkatePhysics( new() );
		var forces = new WheelForce[ 4 ];
		for ( var i = 0; i < 20; i++ )
			physics.Step( Body(), Contacts(), new( FlickY: -1f ), 1f / 60f, forces );
		var pop = physics.Step( Body(), Contacts(), new( FlickY: 1f ), 1f / 60f, forces );
		Assert.Equal( TrickKind.Ollie, pop.Trick );
		Assert.InRange( pop.Impulse.Z / 80f, 4.09f, 4.11f );
		Assert.Equal( 0, pop.WheelCount );
		Assert.All( forces, f => Assert.Equal( 0f, f.Load ) );
		var next = physics.Step( Body(), Contacts(), new( FlickY: 1f ), 1f / 60f, forces );
		Assert.Equal( Vector3.Zero, next.Impulse );
	}

	[Fact]
	public void BailAndResetClearGestureAndPushState()
	{
		var physics = new SkatePhysics( new() );
		var forces = new WheelForce[ 4 ];
		physics.Step( Body(), Contacts(), new( FlickY: -1f ), 1f / 60f, forces );
		physics.Bail();
		var bailed = physics.Step( Body(), Contacts(), new( FlickY: 1f ), 1f / 60f, forces );
		Assert.Equal( MotionMode.Bailed, bailed.Mode );
		Assert.Equal( Vector3.Zero, bailed.Impulse );
		physics.Reset();
		var reset = physics.Step( Body(), Contacts(), default, 1f / 60f, forces );
		Assert.Equal( MotionMode.Rolling, reset.Mode );
		Assert.Equal( TrickKind.None, reset.Trick );
	}

	private static BoardBody Body() => new( new( 0f, 0f, 0.08f ), Quaternion.Identity,
		Vector3.Zero, Vector3.Zero, new( 3f, 5f, 4f ) );

	private static WheelContact[] Contacts() => Enumerable.Range( 0, 4 )
		.Select( i => new WheelContact( true, SkateWheels.Offset( i, new() ),
			Vector3.UnitZ, Vector3.Zero, 0.08f ) ).ToArray();
}
