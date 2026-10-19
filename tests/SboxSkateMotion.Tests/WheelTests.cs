using System.Numerics;
using SkateMotion.Core;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WheelTests
{
	private readonly SkateSettings _settings = new();
	private static BoardBody Body( Vector3 velocity ) =>
		new( new( 0f, 0f, 0.08f ), Quaternion.Identity, velocity, Vector3.Zero, new( 3f, 5f, 4f ) );

	[Fact]
	public void IndependentWheelOffsetsCoverBothAxlesAndBothSides()
	{
		var offsets = Enumerable.Range( 0, 4 ).Select( i => SkateWheels.Offset( i, _settings ) ).ToArray();
		Assert.Equal( 4, offsets.Distinct().Count() );
		Assert.Equal( 2, offsets.Count( p => p.X > 0f ) );
		Assert.Equal( 2, offsets.Count( p => p.Y > 0f ) );
		Assert.Equal( Vector3.Zero, offsets.Aggregate( Vector3.Zero, (a, b) => a + b ) );
	}

	[Fact]
	public void AirAndWallsCannotCreateWheelSupport()
	{
		var body = Body( Vector3.Zero );
		Assert.Equal( default, Solve( body, default ) );
		Assert.Equal( default, Solve( body, Contact( normal: Vector3.UnitX ) ) );
		Assert.Equal( default, Solve( body, Contact( distance: 0.2f ) ) );
	}

	[Theory]
	[InlineData( 0f, 0f, 0f )]
	[InlineData( float.NaN, 0f, 1f )]
	[InlineData( float.PositiveInfinity, 0f, 1f )]
	public void InvalidMapNormalsCannotPoisonPhysics( float x, float y, float z )
	{
		Assert.Equal( default, Solve( Body( Vector3.Zero ), Contact( normal: new( x, y, z ) ) ) );
	}

	[Theory]
	[InlineData( 60 )]
	[InlineData( 120 )]
	[InlineData( 240 )]
	public void PassiveTractionNeverAddsTangentialEnergyAndStaysInsideFrictionCone( int rate )
	{
		var normal = Vector3.Normalize( new Vector3( -0.2f, 0.15f, 1f ) );
		var contact = Contact( normal: normal );
		for ( var i = -4; i <= 4; i++ )
		{
			var body = Body( new Vector3( i, 3f, 0f ) );
			var force = SkateWheels.Solve( body, contact, 0, default, _settings, 1f / rate, 0f, 0f );
			var tangent = force.Force - normal * force.Load;
			Assert.True( Vector3.Dot( tangent, body.Velocity ) <= 0.0001f );
			Assert.True( tangent.Length() <= force.Load * _settings.Grip + 0.001f );
		}
	}

	[Fact]
	public void MatchingMovingPlatformVelocityProducesNoTangentialDrag()
	{
		var speed = new Vector3( 3f, 2f, 0f );
		var force = Solve( Body( speed ), Contact() with { SurfaceVelocity = speed } );
		Assert.Equal( 0f, force.Force.X );
		Assert.Equal( 0f, force.Force.Y );
		Assert.True( force.Load > 0f );
	}

	[Fact]
	public void FrictionlessMapSurfaceStillSupportsBoardWithoutTraction()
	{
		var force = Solve( Body( new( 3f, 2f, 0f ) ), Contact() with { Grip = 0f } );
		Assert.True( force.Load > 0f );
		Assert.Equal( new Vector3( 0f, 0f, force.Load ), force.Force );
	}

	[Fact]
	public void SteeringCreatesYawFromTheFourContactForces()
	{
		var body = Body( new( 4f, 0f, 0f ) );
		var torque = Vector3.Zero;
		for ( var i = 0; i < 4; i++ )
		{
			var point = SkateWheels.Offset( i, _settings );
			var force = SkateWheels.Solve( body, Contact() with { Point = point }, i,
				new( Steer: 1f ), _settings, 1f / 60f, 0f, 0f );
			torque += Vector3.Cross( point - body.Position, force.Force );
		}
		Assert.True( torque.Z > 0f );
	}

	private WheelForce Solve( BoardBody body, WheelContact contact ) =>
		SkateWheels.Solve( body, contact, 0, default, _settings, 1f / 60f, 0f, 0f );

	private static WheelContact Contact( Vector3? normal = null, float distance = 0.08f ) =>
		new( true, Vector3.Zero, normal ?? Vector3.UnitZ, Vector3.Zero, distance );
}
