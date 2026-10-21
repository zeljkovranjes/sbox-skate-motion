using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootCarryIkTests
{
	// pelvis -> thigh -> shin -> ankle -> toe, knee bent forward (+X).
	private static readonly int[] Parents = { -1, 0, 1, 2, 3 };

	private static float Angle( Vector3 a, Vector3 b ) =>
		MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( a ), Vector3.Normalize( b ) ), -1f, 1f ) );

	[Fact]
	public void CarriedFootKeepsItsAngleToTheShin()
	{
		// Lifting the ankle bends the knee and turns the shin; a carried foot turns with it, so the ankle neither folds
		// nor opens, where a foot keeping its orientation changes its angle to the shin by the shin's turn.
		Vector3[] Pose() => new[] { new Vector3( 0f, 0f, 1.1f ), new Vector3( 0f, 0f, 1f ), new Vector3( 0.1f, 0f, 0.5f ), Vector3.Zero,
			new Vector3( 0.15f, 0f, -0.03f ) };
		var start = Pose();
		var before = Angle( start[ 2 ] - start[ 3 ], start[ 4 ] - start[ 3 ] );
		var target = new Vector3( 0f, 0f, 0.25f );

		var carried = Pose();
		var rotations = Enumerable.Repeat( Quaternion.Identity, 5 ).ToArray();
		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( carried, rotations, target, default, true );
		Assert.True( Vector3.Distance( carried[ 3 ], target ) < 0.0001f );
		Assert.Equal( before, Angle( carried[ 2 ] - carried[ 3 ], carried[ 4 ] - carried[ 3 ] ), 3 );
		Assert.Equal( Vector3.Distance( start[ 3 ], start[ 4 ] ), Vector3.Distance( carried[ 3 ], carried[ 4 ] ), 4 );
		Assert.NotEqual( Quaternion.Identity, rotations[ 3 ] );

		var kept = Pose();
		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( kept, Enumerable.Repeat( Quaternion.Identity, 5 ).ToArray(), target );
		Assert.True( MathF.Abs( Angle( kept[ 2 ] - kept[ 3 ], kept[ 4 ] - kept[ 3 ] ) - before ) > 0.05f );
	}
}
