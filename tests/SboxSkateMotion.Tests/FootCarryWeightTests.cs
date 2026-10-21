using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootCarryWeightTests
{
	// pelvis -> thigh -> shin -> ankle -> toe, knee bent forward (+X).
	private static readonly int[] Parents = { -1, 0, 1, 2, 3 };

	private static Vector3[] Pose() => new[] { new Vector3( 0f, 0f, 1.1f ), new Vector3( 0f, 0f, 1f ), new Vector3( 0.1f, 0f, 0.5f ),
		Vector3.Zero, new Vector3( 0.15f, 0f, -0.03f ) };

	private static float AnkleAngle( Vector3[] p ) =>
		MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( p[ 2 ] - p[ 3 ] ), Vector3.Normalize( p[ 4 ] - p[ 3 ] ) ), -1f, 1f ) );

	private static Vector3[] Solved( float carry )
	{
		var pose = Pose();
		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( pose, Enumerable.Repeat( Quaternion.Identity, 5 ).ToArray(), new Vector3( 0f, 0f, 0.25f ),
			default, carry );
		return pose;
	}

	[Fact]
	public void AFootTurnsWithTheShinByItsCarryWeight()
	{
		// Carried by nothing, half, or all of the shin's turn: the ankle angle moves evenly between the two, so a foot
		// coming off a rail (where it turns with the shin) never opens or closes its ankle by the whole turn in a frame.
		var none = AnkleAngle( Solved( 0f ) );
		var half = AnkleAngle( Solved( 0.5f ) );
		var all = AnkleAngle( Solved( 1f ) );
		Assert.True( MathF.Abs( all - none ) > 0.05f );
		Assert.True( MathF.Abs( half - (none + all) * 0.5f ) < MathF.Abs( all - none ) * 0.2f, $"{none} {half} {all}" );
		// The whole weights give what the switch gave.
		var carried = Pose();
		new TwoBoneIk( Parents, 1, 2, 3 ).Solve( carried, Enumerable.Repeat( Quaternion.Identity, 5 ).ToArray(), new Vector3( 0f, 0f, 0.25f ),
			default, true );
		Assert.Equal( AnkleAngle( carried ), all, 4 );
	}
}
