using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GeneratedContactFrictionTests
{
	[Fact]
	public void GeneratedProjectionPreservesUnorderedCoefficientsAndPreviousNormalLimit()
	{
		var previous = new Vector4( 2, 0, 0, 0 );
		var candidate = new Vector4( 9, 1, -1, -2 );
		Assert.Throws<ArgumentOutOfRangeException>( () => ContactImpulse.Project( previous, candidate, .25f, .75f ) );
		Assert.Equal( new Vector4( 9, 1.5f, -1.5f, 0 ),
			ContactImpulse.ProjectGenerated( previous, candidate, .25f, .75f ) );
	}

	[Fact]
	public void GeneratedProjectionStillRejectsUnsupportedNumericInputs()
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => ContactImpulse.ProjectGenerated( Vector4.Zero, Vector4.One, -.1f, .5f ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ContactImpulse.ProjectGenerated( Vector4.Zero, Vector4.One, .1f, float.NaN ) );
	}

	[Fact]
	public void GeneratedWorldRouteSolvesAndReportsUnorderedFriction()
	{
		var first = Body();
		var second = Body();
		first.Velocity = -Vector3.UnitZ;
		var settings = new BodyDynamicsSettings( Vector3.One, 0, 0, 1000, 1000, 1, 0, 0 );
		var world = new PhysicalSolverWorld( [first, second], [settings, settings], [0, 1] );
		var owner = new PhysicalContactOwner();
		world.RegisterContactBody( first, owner, 5 );
		world.Prepare();
		var contact = new CollisionContact( 0, 1, Vector3.Zero, Vector3.UnitZ * .1f,
			Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, .1f, .8f, 0 );
		var contacts = new[] { world.CompileContact( contact, .1f ) };
		var output = new PhysicalWorldContact[ 1 ];
		var count = world.SolveGeneratedIntegrateAndCollect( contacts, [], [], output, 4, .1f, Vector3.Zero );
		Assert.Equal( 1, count );
		Assert.Equal( 1, owner.Count );
		Assert.True( contacts[ 0 ].Impulse.X > 0 );
		Assert.Equal( .1f, contacts[ 0 ].StaticFriction );
		Assert.Equal( .8f, contacts[ 0 ].SlidingFriction );
		Assert.NotEqual( Vector3.Zero, first.Pose.Translation );
	}

	private static PhysicalBodyState Body() => new()
	{
		Pose = new LimbRigidTransform( new LimbFrame( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ ), Vector3.Zero ),
		Orientation = Vector4.UnitW,
		MotionMode = 4,
		InverseMass = 1,
		InverseInertiaDiagonal = Vector3.One
	};
}
