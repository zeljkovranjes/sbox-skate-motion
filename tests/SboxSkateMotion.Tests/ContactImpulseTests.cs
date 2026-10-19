using System.Numerics;
using SkateMotion.Core.Constraints;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ContactImpulseTests
{
	[Theory]
	[InlineData( 0f, 1f, 0f, 0f, 0.5f, 0.5f, 1f, 0f, 0f )]
	[InlineData( 0f, -1f, 0f, 0f, 0.5f, 0.5f, 0f, 0f, 0f )]
	[InlineData( 4f, 4f, 10f, 0f, 0.5f, 0.5f, 4f, 2f, 0f )]
	[InlineData( 0f, 1f, 10f, 0f, 0.5f, 0.5f, 1f, 0f, 0f )]
	[InlineData( 1f, 1f, 10f, 0f, 0.5f, 0.5f, 1f, 0.5f, 0f )]
	[InlineData( 4f, 4f, 2.5f, 0f, 0.8f, 0.5f, 4f, 2.5f, 0f )]
	[InlineData( 4f, 4f, 10f, 0f, 0.8f, 0.5f, 4f, 2f, 0f )]
	[InlineData( 0f, 0f, 0f, 2f, 0.5f, 0.5f, 0f, 0f, 2f )]
	[InlineData( 0f, 0f, 0f, -2f, 0.5f, 0.5f, 0f, 0f, 0f )]
	[InlineData( 4f, 4f, 3f, 0f, 0.75f, 0.5f, 4f, 3f, 0f )]
	public void MatchesSyntheticInstructionProbes( float oldNormal, float normal, float tangent,
		float separation, float staticFriction, float slidingFriction,
		float expectedNormal, float expectedTangent, float expectedSeparation )
	{
		var actual = ContactImpulse.Project( new Vector4( oldNormal, 0f, 0f, 0f ),
			new Vector4( normal, tangent, -tangent, separation ), staticFriction, slidingFriction );

		Assert.Equal( new Vector4( expectedNormal, expectedTangent, -expectedTangent, expectedSeparation ), actual );
	}

	[Fact]
	public void RejectsNonFiniteSolverState()
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => ContactImpulse.Project(
			Vector4.Zero, new Vector4( float.NaN ), 0.8f, 0.5f ) );
	}
}
