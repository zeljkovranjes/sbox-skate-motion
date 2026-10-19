using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class JointLinearPrecisionTests
{
	[Fact]
	public void StoredPhysicalAxesDetermineAnchorOffsets()
	{
		var a = Body( 0f, 1f, 1f );
		var b = Body( 0f, 1f, 1f );
		a.RotationBasis = new Matrix4x4( 0f, 1f, 0f, 0f, -1f, 0f, 0f, 0f,
			0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f );
		var geometry = new JointGeometry( 0, 1, Vector3.UnitX, Vector3.Zero,
			Quaternion.Identity, Vector3.Zero, Vector3.One );
		var result = JointLinearCompiler.Compile( a, b, geometry, .01f );
		Assert.Equal( Vector3.UnitY, result.Pair.OffsetA );
		Assert.Equal( Quaternion.Identity, a.Rotation );
	}

	[Fact]
	public void LinearResponseAccumulatesMassBeforeDirectionalContributions()
	{
		var a = Body( .12, .375f, 1.75f );
		var b = Body( .06, .25f, 1.25f );
		var geometry = new JointGeometry( 0, 1, Vector3.Zero, new( .19f, .02f, .11f ),
			Quaternion.Identity, Vector3.Zero, new( 120f, 180f, 240f ) );
		var result = JointLinearCompiler.Compile( a, b, geometry, 1f / 240f );
		Assert.Equal( 1.491146206855774f, result.Pair.LinearCoefficients.M33 );
	}

	private static DynamicBody Body( double angle, float inverseMass, float inertia )
	{
		var c = (float)Math.Cos( angle );
		var s = (float)Math.Sin( angle );
		return new DynamicBody
		{
			Rotation = new( 0f, 0f, (float)Math.Sin( angle / 2 ), (float)Math.Cos( angle / 2 ) ),
			RotationBasis = new Matrix4x4( c, s, 0f, 0f, -s, c, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f ),
			InverseMass = inverseMass, WorldInverseInertia = Matrix4x4.CreateScale( inertia )
		};
	}
}
