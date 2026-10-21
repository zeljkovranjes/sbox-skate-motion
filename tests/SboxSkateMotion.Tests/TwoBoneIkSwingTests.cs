using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TwoBoneIkSwingTests
{
	// shoulder -> upper arm -> forearm -> hand -> finger, the elbow bent down (-Z) under the line to the hand (+X).
	private static readonly int[] Parents = { -1, 0, 1, 2, 3 };

	private static (Vector3[] Positions, Quaternion[] Rotations) Arm()
	{
		var positions = new[] { Vector3.Zero, new Vector3( 0.05f, 0f, 0f ), new Vector3( 0.3f, 0f, -0.1f ), new Vector3( 0.55f, 0f, 0f ),
			new Vector3( 0.62f, 0f, 0f ) };
		var hand = Quaternion.CreateFromAxisAngle( Vector3.UnitX, 0.4f );
		return (positions, new[] { Quaternion.Identity, Quaternion.Identity, Quaternion.Identity, hand, hand });
	}

	[Fact]
	public void AnElbowSwungFurtherThanAllowedIsTurnedBackAndTheHandStays()
	{
		// The elbow points down (-Z) from the shoulder-to-hand line; a moment ago it pointed along -Y, a quarter turn away.
		var (positions, rotations) = Arm();
		var hand = positions[ 3 ];
		var finger = positions[ 4 ];
		var handTurn = rotations[ 3 ];
		var upper = Vector3.Distance( positions[ 1 ], positions[ 2 ] );
		var lower = Vector3.Distance( positions[ 2 ], positions[ 3 ] );

		var pole = new TwoBoneIk( Parents, 1, 2, 3 ).LimitSwing( positions, rotations, -Vector3.UnitY, 0.2f );

		var angle = MathF.Acos( Math.Clamp( Vector3.Dot( pole, -Vector3.UnitY ), -1f, 1f ) );
		Assert.Equal( 0.2f, angle, 3 );
		Assert.True( Vector3.Distance( positions[ 3 ], hand ) < 1e-5f, "The hand must stay where it was." );
		Assert.True( Vector3.Distance( positions[ 4 ], finger ) < 1e-5f, "What hangs from the hand must stay too." );
		Assert.Equal( handTurn, rotations[ 3 ] );
		Assert.Equal( upper, Vector3.Distance( positions[ 1 ], positions[ 2 ] ), 4 );
		Assert.Equal( lower, Vector3.Distance( positions[ 2 ], positions[ 3 ] ), 4 );
		Assert.True( positions[ 2 ].Y < -0.01f, "The elbow must have turned back toward where it pointed." );
	}

	[Fact]
	public void AnElbowWithinTheAllowedSwingIsLeftAlone()
	{
		var (positions, rotations) = Arm();
		var elbow = positions[ 2 ];
		var was = Vector3.Normalize( new Vector3( 0f, -0.1f, -1f ) );

		var pole = new TwoBoneIk( Parents, 1, 2, 3 ).LimitSwing( positions, rotations, was, 0.5f );

		Assert.Equal( elbow, positions[ 2 ] );
		Assert.True( Vector3.Dot( pole, -Vector3.UnitZ ) > 0.999f );
	}
}
