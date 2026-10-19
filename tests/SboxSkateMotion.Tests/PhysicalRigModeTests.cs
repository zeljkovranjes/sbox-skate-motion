using System.Numerics;
using System.Text.Json;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalRigModeTests
{
	public static IEnumerable<object[]> Cases => Enumerable.Range( 0, 36 ).Select( i => new object[] { i } );

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void BodyTransitionsMatchCapturedMassTensorDampingAndCollisionState( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var modes = Create( fixture );
		var poses = modes.Bodies.Select( b => b.Body.Pose ).ToArray();
		Apply( modes, fixture.GetProperty( "profile" ) );
		Assert.Equal( fixture.GetProperty( "selectedMask" ).GetInt32() != 0, modes.PairMask.Secondary );
		Assert.Equal( fixture.GetProperty( "aggregateFlags" ).GetUInt32(), modes.AggregateFlags );
		Assert.Equal( fixture.GetProperty( "ownerFlags" )[ 0 ].GetInt32() != 0, modes.PrimaryPending );
		Assert.Equal( fixture.GetProperty( "ownerFlags" )[ 1 ].GetInt32() != 0, modes.Secondary );
		for ( var i = 0; i < 3; ++i )
			Assert.Equal( Vector( fixture.GetProperty( "ownerProperties" ), i * 3 ), modes.ContactProperties[ i ] );
		for ( var i = 0; i < 26; ++i )
		{
			var body = modes.Bodies[ i ];
			var expected = fixture.GetProperty( "outputs" )[ i ];
			var properties = expected.GetProperty( "properties" );
			Assert.Equal( Vector( properties ), body.Body.Mass.Value.InverseInertia );
			Assert.Equal( properties[ 4 ].GetSingle(), body.Body.Mass.Value.InverseMass );
			Assert.Equal( properties[ 5 ].GetSingle(), body.MaximumInertia );
			Assert.Equal( properties[ 8 ].GetSingle(), body.LinearDamping );
			Assert.Equal( properties[ 9 ].GetSingle(), body.AngularDamping );
			Assert.Equal( Vector( expected.GetProperty( "worldDiagonal" ) ), body.Body.InverseInertiaDiagonal );
			Assert.Equal( Vector( expected.GetProperty( "worldOffDiagonal" ) ), body.Body.InverseInertiaOffDiagonal );
			Assert.Equal( expected.GetProperty( "worldInverseMass" ).GetSingle(), body.Body.InverseMass );
			Assert.Equal( expected.GetProperty( "shapeFlags" ).GetUInt32(), body.ShapeFlags );
			Assert.Equal( expected.GetProperty( "shapeMode" ).GetUInt32(), body.ShapeMode );
			Assert.Equal( expected.GetProperty( "contactPropertySlot" ).GetInt32(), body.ContactPropertySlot );
			if ( i < 24 )
				Assert.Equal( fixture.GetProperty( "ownerCounters" )[ i ].GetUInt32(), body.CollisionCounter );
			Assert.Equal( poses[ i ], body.Body.Pose );
			Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), body.Body.Orientation );
			Assert.Equal( new Vector3( 6f, 7f, 8f ), body.Body.Velocity );
			Assert.Equal( 4, body.Body.MotionMode );
		}
	}

	[Theory]
	[MemberData( nameof( Cases ) )]
	public void JointRefreshMatchesCapturedRangesAndPreservesFramesSpeedsAndLocks( int index )
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var fixture = document.RootElement.GetProperty( "cases" )[ index ];
		var profile = fixture.GetProperty( "profile" );
		var alternate = profile.GetProperty( "secondary" ).GetBoolean() && !profile.GetProperty( "primaryLimits" ).GetBoolean();
		foreach ( var sample in fixture.GetProperty( "joints" ).EnumerateArray() )
		{
			var authored = sample.GetProperty( "authored" );
			var scales = sample.GetProperty( "scales" );
			var expected = sample.GetProperty( "expected" );
			var limits = new PhysicalStructuralJointLimits( new( 2f, 3f, 4f, 5f ), 6f, 7f, 0f, 0f, 1f, 1f, 4, 2 );
			var joint = new PhysicalStructuralJoint( sample.GetProperty( "parent" ).GetInt32(), sample.GetProperty( "child" ).GetInt32(),
				new( Vector4.UnitW, Vector3.One, Vector4.UnitY, Vector3.UnitX ), Vector4.UnitZ, limits );
			var refreshed = PhysicalStructuralJointRanges.Refresh( joint,
				new( Vector4.UnitW, Vector4.UnitW, Vector4.UnitW, Vector3.Zero, authored[ 0 ].GetSingle(), authored[ 1 ].GetSingle() ),
				new( scales[ 0 ].GetSingle(), scales[ 1 ].GetSingle(), scales[ 2 ].GetSingle(), scales[ 3 ].GetSingle() ),
				alternate, 1.3f, .7f, Math() );
			Assert.Equal( expected[ 0 ].GetSingle(), refreshed.Limits.SwingAngle );
			Assert.Equal( expected[ 1 ].GetSingle(), refreshed.Limits.TwistAngle );
			Assert.Equal( expected[ 2 ].GetSingle(), refreshed.Limits.SwingCosine );
			Assert.Equal( expected[ 3 ].GetSingle(), refreshed.Limits.TwistCosine );
			Assert.Equal( joint, refreshed with { Limits = limits } );
			Assert.Equal( limits, refreshed.Limits with { SwingAngle = 0f, TwistAngle = 0f, SwingCosine = 1f, TwistCosine = 1f } );
		}
	}

	[Fact]
	public void ReturningToPrimaryKeepsThePendingFlagClearedAndRestoresThePrimaryMasses()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var modes = Create( document.RootElement.GetProperty( "cases" )[ 0 ], true );
		modes.ApplySecondary( new( true, true, true, .37f, .19f, .41f, .27f ) );
		modes.ApplyPrimary( true );
		Assert.False( modes.PrimaryPending );
		Assert.False( modes.Secondary );
		Assert.True( modes.PairMask.SuppressAll );
		Assert.False( modes.PairMask.Secondary );
		Assert.Equal( .99f, modes.ContactProperties[ 1 ].Z );
		foreach ( var body in modes.Bodies.Skip( 1 ).Take( 23 ) )
		{
			Assert.Equal( body.Primary, body.Body.Mass );
			Assert.Equal( 0u, body.ShapeMode );
			Assert.Equal( 5u, body.ShapeFlags );
			Assert.Equal( 0f, body.LinearDamping );
			Assert.Equal( 0f, body.AngularDamping );
		}
	}

	[Fact]
	public void RefreshedPropertiesReachTheNextWorldIntegrationInWorldIndexOrder()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var modes = Create( document.RootElement.GetProperty( "cases" )[ 0 ] );
		var bodies = modes.Bodies.Select( b => b.Body ).Reverse().ToArray();
		var settings = new BodyDynamicsSettings( Vector3.One, 2f, 3f, 100f, 100f, 5f, 0f, 0 );
		var world = new PhysicalSolverWorld( bodies, Enumerable.Repeat( settings, 26 ).ToArray(), Enumerable.Range( 0, 26 ).ToArray() );
		modes.ApplySecondary( new( true, true, false, .37f, .19f, .41f, .27f ) );
		world.RefreshRigBodyProperties( modes );
		var expected = bodies.Select( PhysicalSolverBody.Read ).ToArray();
		for ( var i = 0; i < 26; ++i )
		{
			var mode = modes.Bodies[ 25 - i ];
			var workspace = new SolverBody { InverseMass = expected[ i ].InverseMass };
			BodyDynamics.Step( ref expected[ i ], ref workspace, settings with
			{
				LocalInverseInertia = mode.Body.Mass.Value.InverseInertia,
				LinearDamping = 22.19999885559082f,
				AngularDamping = 11.399998664855957f,
				AngularMotionWeight = mode.MaximumInertia
			}, 1f / 60f, Vector3.Zero );
		}
		world.Prepare();
		world.SolveAndIntegrate( [], [], [], 1, 1f / 60f, Vector3.Zero );
		for ( var i = 0; i < 26; ++i )
		{
			Assert.Equal( expected[ i ].Velocity, bodies[ i ].Velocity );
			Assert.Equal( expected[ i ].AngularVelocity, bodies[ i ].AngularVelocity );
			Assert.Equal( expected[ i ].WorldInverseInertia, world.Body( i ).WorldInverseInertia );
			Assert.Equal( expected[ i ].MotionMetric, bodies[ i ].MotionMetric );
		}
	}

	[Fact]
	public void PropertyRefreshRejectsForeignBodiesAndPreparedWorlds()
	{
		using var document = JsonDocument.Parse( PhysicalRigModeFixtures.Json );
		var modes = Create( document.RootElement.GetProperty( "cases" )[ 0 ] );
		var bodies = modes.Bodies.Select( b => b.Body ).ToArray();
		var world = new PhysicalSolverWorld( bodies, new BodyDynamicsSettings[ 26 ], [] );
		var foreign = new PhysicalSolverWorld( bodies.Take( 25 ).ToArray(), new BodyDynamicsSettings[ 25 ], [] );
		Assert.Throws<ArgumentException>( () => foreign.RefreshRigBodyProperties( modes ) );
		world.Prepare();
		Assert.Throws<InvalidOperationException>( () => world.RefreshRigBodyProperties( modes ) );
	}

	private static PhysicalRigBodyModes Create( JsonElement fixture, bool suppressAll = false )
	{
		var bodies = new PhysicalRigModeBody[ 26 ];
		for ( var i = 0; i < bodies.Length; ++i )
		{
			var input = fixture.GetProperty( "inputs" )[ i ];
			var pose = input.GetProperty( "pose" );
			var state = new PhysicalBodyState
			{
				Pose = new( new( Vector( pose[ 0 ] ), Vector( pose[ 1 ] ), Vector( pose[ 2 ] ) ), Vector( pose[ 3 ] ) ),
				Orientation = new( 2f, 3f, 4f, 5f ), Velocity = new( 6f, 7f, 8f ), AngularVelocity = new( .1f, .2f, .3f ), MotionMode = 4,
				Mass = new( Vector( input.GetProperty( "initialInertia" ) ), input.GetProperty( "initialInverseMass" ).GetSingle() ),
				InverseInertiaDiagonal = new( 9f, 4f, 5f ), InverseInertiaOffDiagonal = new( 8f, 7f, 3f ), InverseMass = 6f
			};
			bodies[ i ] = new( state,
				new( Vector( input.GetProperty( "primaryInertia" ) ), input.GetProperty( "primaryInverseMass" ).GetSingle() ),
				new( Vector( input.GetProperty( "alternateInertia" ) ), input.GetProperty( "alternateInverseMass" ).GetSingle() ) )
			{
				MaximumInertia = .33f, LinearDamping = 15f, AngularDamping = 16f, ShapeFlags = (uint)(0x31 + i),
				ShapeMode = (uint)(0x41 + i), ContactPropertySlot = 3, CollisionCounter = (uint)(i % 3)
			};
		}
		return new( bodies, [ new( .5f, 8f, 30f ), new( .6f, 9f, 31f ), new( .7f, 10f, 32f ) ], suppressAll ) { PrimaryPending = true };
	}

	private static void Apply( PhysicalRigBodyModes modes, JsonElement profile )
	{
		if ( profile.GetProperty( "secondary" ).GetBoolean() )
			modes.ApplySecondary( new( profile.GetProperty( "alternateMass" ).GetBoolean(), profile.GetProperty( "alternateInertia" ).GetBoolean(),
				profile.GetProperty( "specialFriction" ).GetBoolean(), .37f, .19f, .41f, .27f ) );
		else
			modes.ApplyPrimary( profile.GetProperty( "primaryCollision" ).GetBoolean() );
	}

	private static Vector3 Vector( JsonElement v, int start = 0 ) => new( v[ start ].GetSingle(), v[ start + 1 ].GetSingle(), v[ start + 2 ].GetSingle() );

	private static LimbSolverMath Math() => new(
		[ -.0013602249091491103f, .04165669530630112f, -.4999990165233612f ],
		[ -.0001950727018993348f, .00833207555115223f, -.16666652262210846f ], new float[ 8 ],
		.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f );
}
