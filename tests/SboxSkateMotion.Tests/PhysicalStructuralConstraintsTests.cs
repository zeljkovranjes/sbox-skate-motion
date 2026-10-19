using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalStructuralConstraintsTests
{
	[Fact]
	public void CompilationUsesResolvedEndpointsAndIndependentSpeedChannels()
	{
		var world = World();
		world.Prepare();
		var result = world.CompileStructuralJoint( Joint(), 1, 0, .1f );
		Assert.Equal( 1, result.Pair.BodyA );
		Assert.Equal( 0, result.Pair.BodyB );
		Assert.InRange( Vector3.Distance( new( -.05f, -.1f, -.1f ), result.PositiveAngularBias ), 0f, 1e-6f );
		Assert.InRange( Vector3.Distance( new( .05f, .1f, .1f ), result.NegativeAngularBias ), 0f, 1e-6f );
	}

	[Fact]
	public void CompilationRequiresFreshWorldPreparation()
	{
		var world = World();
		Assert.Throws<InvalidOperationException>( () => world.CompileStructuralJoint( Joint(), 0, 1, .1f ) );
		world.Prepare();
		world.SolveAndIntegrate( [], [], [], 0, .1f, Vector3.Zero );
		Assert.Throws<InvalidOperationException>( () => world.CompileStructuralJoint( Joint(), 0, 1, .1f ) );
	}

	[Fact]
	public void CanonicalBoneNumbersCannotSubstituteForWorldEndpoints()
	{
		var world = World();
		world.Prepare();
		Assert.Throws<ArgumentOutOfRangeException>( () => world.CompileStructuralJoint( Joint(), 17, 9, .1f ) );
	}

	private static PhysicalStructuralJoint Joint() => new( 9, 17,
		new( Vector4.UnitW, Vector3.Zero, Vector4.UnitW, Vector3.Zero ), Vector4.UnitW,
		new( Vector4.One, 2f, 4f, .2f, .3f, MathF.Cos( .2f ), MathF.Cos( .3f ), 4, 2 ) );

	private static PhysicalSolverWorld World()
	{
		PhysicalBodyState Body() => new()
		{
			Pose = LimbRigidTransform.Identity, Orientation = Vector4.UnitW, MotionMode = 4,
			InverseMass = 1f, InverseInertiaDiagonal = Vector3.One
		};
		return new( [ Body(), Body() ], new BodyDynamicsSettings[ 2 ], [] );
	}
}
