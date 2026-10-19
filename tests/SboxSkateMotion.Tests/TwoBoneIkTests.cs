using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TwoBoneIkTests
{
	// pelvis -> thigh -> shin -> ankle -> toe, knee bent forward (+X).
	private static readonly int[] Parents = { -1, 0, 1, 2, 3 };

	[Fact]
	public void LiftingTheAnkleKeepsLengthsKneeSideAndFootOrientation()
	{
		var positions = new[] { new Vector3( 0f, 0f, 1.1f ), new Vector3( 0f, 0f, 1f ), new Vector3( 0.1f, 0f, 0.5f ), Vector3.Zero, new Vector3( 0.1f, 0f, -0.05f ) };
		var footRotation = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.3f );
		var rotations = new[] { Quaternion.Identity, Quaternion.Identity, Quaternion.Identity, footRotation, footRotation };
		var thigh = Vector3.Distance( positions[ 1 ], positions[ 2 ] );
		var shin = Vector3.Distance( positions[ 2 ], positions[ 3 ] );
		var toe = positions[ 4 ] - positions[ 3 ];

		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( positions, rotations, new Vector3( 0f, 0f, 0.2f ) );

		Assert.True( Vector3.Distance( positions[ 3 ], new Vector3( 0f, 0f, 0.2f ) ) < 0.0001f );
		Assert.Equal( thigh, Vector3.Distance( positions[ 1 ], positions[ 2 ] ), 4 );
		Assert.Equal( shin, Vector3.Distance( positions[ 2 ], positions[ 3 ] ), 4 );
		Assert.True( positions[ 2 ].X > 0.2f, "The knee must keep bending forward." );
		Assert.Equal( footRotation, rotations[ 3 ] );
		Assert.True( Vector3.Distance( toe, positions[ 4 ] - positions[ 3 ] ) < 0.0001f );
	}

	[Theory]
	[InlineData( 0.002f )]
	[InlineData( -0.002f )]
	[InlineData( -0.06f )]
	public void ANearlyStraightOrBackwardKneeBendsTowardTheHint( float kneeOffset )
	{
		var positions = new[] { new Vector3( 0f, 0f, 1.1f ), new Vector3( 0f, 0f, 1f ), new Vector3( kneeOffset, 0f, 0.5f ), Vector3.Zero, new Vector3( 0.1f, 0f, -0.05f ) };
		var rotations = Enumerable.Repeat( Quaternion.Identity, 5 ).ToArray();

		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( positions, rotations, new Vector3( 0f, 0f, 0.3f ), Vector3.UnitX );

		Assert.True( positions[ 2 ].X > 0.1f, "The knee must bend toward the toes." );
		Assert.True( Vector3.Distance( positions[ 3 ], new Vector3( 0f, 0f, 0.3f ) ) < 0.0001f );
	}
}
