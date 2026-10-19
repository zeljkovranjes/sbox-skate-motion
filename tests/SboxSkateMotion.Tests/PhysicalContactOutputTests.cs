using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalContactOutputTests
{
	[Theory]
	[InlineData( 4, 4, false )]
	[InlineData( 12, 4, true )]
	[InlineData( 4, 12, true )]
	[InlineData( 4, 8, true )]
	[InlineData( 4, 16, false )]
	public void ReportingFlagSurvivesCompilationIndependentlyOfResponse( int firstMode, int secondMode, bool report )
	{
		var a = Body( Vector3.Zero, firstMode );
		var b = Body( Vector3.UnitY, secondMode );
		var world = World( [a, b], [] );
		world.Prepare();
		var collision = new CollisionContact( 0, 1, Vector3.Zero, Vector3.UnitY,
			Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, .5f, .25f, 0f ) { Materials = 0x1234abcd };
		var contact = world.CompileContact( collision, .1f );
		Assert.Equal( report, contact.ReportContact );
		Assert.Equal( 0x1234abcdu, contact.Materials );
		Assert.Equal( (secondMode & 4) != 0 ? 1f : 0f, contact.InverseMassB );
	}

	[Fact]
	public void ExportSeparatesNormalFromTangentialForceAndIgnoresSeparationImpulse()
	{
		var a = Body( Vector3.Zero );
		var b = Body( new( 4f, 0f, 0f ) );
		var contact = Contact();
		contact.Impulse = new( 2f, -3f, 4f, 999f );
		contact.InverseMassA = 1f;
		contact.InverseMassB = 3f;
		Assert.True( PhysicalContactOutput.TryExport( contact, a, b, 2f, out var output ) );
		Assert.Equal( new Vector4( 3f, 0f, 0f, 0f ), output.Point );
		Assert.Equal( new Vector3( 0f, 8f, 0f ), output.NormalImpulse );
		Assert.Equal( new Vector3( -12f, 0f, 16f ), output.Impulse );
		Assert.Equal( 0x1234abcdu, output.Materials );
	}

	[Theory]
	[InlineData( 0f, true )]
	[InlineData( -1f, true )]
	[InlineData( 1f, false )]
	public void UnloadedOrUnrequestedContactsDoNotReachRiderFeedback( float load, bool report )
	{
		var contact = Contact();
		contact.ReportContact = report;
		contact.Impulse = new( load, 100f, 100f, 100f );
		Assert.False( PhysicalContactOutput.TryExport( contact, Body( Vector3.Zero ), Body( Vector3.Zero ), 60f, out _ ) );
	}

	[Fact]
	public void WorldCollectsAfterIntegrationAndClearsOnTheNextEmptyStep()
	{
		var a = Body( Vector3.Zero );
		var b = Body( new( 4f, 0f, 0f ) );
		a.Velocity = new( 2f, 0f, 0f );
		var world = World( [a, b], [0] );
		var owner = new PhysicalContactOwner();
		world.ContactCollector.Register( a, owner, 7 );
		var contact = Contact();
		contact.Impulse = new( 1f, 0f, 0f, 0f );
		var output = new PhysicalWorldContact[ 1 ];
		world.Prepare();
		Assert.Equal( 1, world.SolveIntegrateAndCollect( [contact], [], [], output, 0, .5f, Vector3.Zero ) );
		Assert.Equal( new Vector3( 1f, 0f, 0f ), a.Pose.Translation );
		Assert.Equal( new Vector4( 2.5f, 0f, 0f, 0f ), output[ 0 ].Point );
		Assert.Equal( 1, owner.Count );
		Assert.Equal( 7, owner.Contact( 0 ).RiderContact.Bone );
		Assert.Equal( Vector3.Zero, owner.Contact( 0 ).RiderContact.Impulse );
		world.Prepare();
		world.SolveIntegrateAndCollect( [], [], [], output, 0, .5f, Vector3.Zero );
		Assert.Equal( 0, owner.Count );
	}

	[Fact]
	public void InsufficientOutputCapacityDoesNotConsumeThePreparedStep()
	{
		var world = World( [Body( Vector3.Zero ), Body( Vector3.UnitY )], [] );
		world.Prepare();
		Assert.Throws<ArgumentException>( () => world.SolveIntegrateAndCollect( [Contact()], [], [], [], 0, .1f, Vector3.Zero ) );
		Assert.Equal( 0, world.SolveIntegrateAndCollect( [Contact()], [], [], new PhysicalWorldContact[ 1 ], 0, .1f, Vector3.Zero ) );
	}

	private static ContactConstraint Contact() => new()
	{
		BodyA = 0, BodyB = 1, ReportContact = true, Materials = 0x1234abcd,
		InverseMassA = 1f, InverseMassB = 1f,
		Normal = Vector3.UnitY, TangentA = Vector3.UnitX, TangentB = Vector3.UnitZ
	};

	private static PhysicalBodyState Body( Vector3 position, int mode = 12 ) => new()
	{
		Pose = new( LimbRigidTransform.Identity.Rotation, position ), Orientation = Vector4.UnitW,
		MotionMode = mode, InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
	};

	private static PhysicalSolverWorld World( PhysicalBodyState[] bodies, int[] order )
	{
		var settings = new BodyDynamicsSettings[ bodies.Length ];
		Array.Fill( settings, new( Vector3.One, 0f, 0f, 100f, 100f, 1f, 0f, 0 ) );
		return new( bodies, settings, order );
	}
}
