using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LegStabilityTests
{
	[Fact]
	public void StraightVerticalLegWithoutHintKeepsFiniteTransforms()
	{
		var positions = new[] { Vector3.UnitZ, new Vector3( 0f, 0f, .5f ), Vector3.Zero, new Vector3( .1f, 0f, 0f ) };
		var rotations = Enumerable.Repeat( Quaternion.Identity, 4 ).ToArray();
		new TwoBoneIk( new[] { -1, 0, 1, 2 }, 0, 1, 2 ).Solve( positions, rotations, new Vector3( 0f, 0f, .2f ) );
		Assert.All( positions, p => Assert.True( float.IsFinite( p.X + p.Y + p.Z ) ) );
		Assert.All( rotations, r => Assert.True( float.IsFinite( r.X + r.Y + r.Z + r.W ) ) );
		Assert.True( Vector3.Distance( positions[ 2 ], new Vector3( 0f, 0f, .2f ) ) < 1e-5f );
		Assert.Equal( .5f, Vector3.Distance( positions[ 0 ], positions[ 1 ] ), 4 );
		Assert.Equal( .5f, Vector3.Distance( positions[ 1 ], positions[ 2 ] ), 4 );
	}

	[Fact]
	public void ATargetAtTheHipLeavesThePoseIntact()
	{
		var positions = new[] { Vector3.UnitZ, new Vector3( .1f, 0f, .5f ), Vector3.Zero };
		var before = positions.ToArray();
		var rotations = Enumerable.Repeat( Quaternion.Identity, 3 ).ToArray();
		new TwoBoneIk( new[] { -1, 0, 1 }, 0, 1, 2 ).Solve( positions, rotations, positions[ 0 ] );
		Assert.Equal( before, positions );
		Assert.All( rotations, rotation => Assert.Equal( Quaternion.Identity, rotation ) );
	}

	[Fact]
	public void KneeDirectionDoesNotSnapAcrossTheStraightLegHintThreshold()
	{
		Vector3 Solve( float kneeOffset )
		{
			var positions = new[] { Vector3.UnitZ, new Vector3( kneeOffset, 0f, .5f ), Vector3.Zero };
			var rotations = Enumerable.Repeat( Quaternion.Identity, 3 ).ToArray();
			new TwoBoneIk( new[] { -1, 0, 1 }, 0, 1, 2 ).Solve( positions, rotations, new Vector3( 0f, 0f, .2f ), Vector3.UnitY );
			return positions[ 1 ];
		}
		Assert.True( Vector3.Distance( Solve( .075f ), Solve( .077f ) ) < .02f,
			"A 2 mm pose change must not switch the knee to a different bend plane." );
	}
}
