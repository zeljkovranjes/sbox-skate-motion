using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class RetargetedAttachmentTests
{
	[Theory]
	[InlineData( false, 1f )]
	[InlineData( true, 1f )]
	[InlineData( false, 27f )]
	[InlineData( true, 27f )]
	public void PreservesAuthoredGripOnADifferentSizedHand( bool mirror, float scale )
	{
		var prop = new Vector3( 2f, 3f, 4f );
		var hand = new Vector3( 1f, 5f, 6f );
		var target = new Vector3( 20f, 30f, 40f );
		var result = RetargetedAttachment.Place( prop, Quaternion.Identity, hand, target, scale, mirror );
		Assert.Equal( target + new Vector3( 1f, mirror ? 2f : -2f, -2f ) * scale, result.Position );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void CarriesTheAuthoredOrientationThroughReflection( bool mirror )
	{
		var rotation = Quaternion.CreateFromYawPitchRoll( 0.6f, -0.4f, 0.8f );
		var point = new Vector3( 1f, 2f, 3f );
		var result = RetargetedAttachment.Place( Vector3.Zero, rotation, Vector3.Zero, Vector3.Zero, 1f, mirror );
		var expected = Vector3.Transform( point, rotation );
		if ( mirror )
		{
			point.Y = -point.Y;
			expected.Y = -expected.Y;
		}
		Assert.True( Vector3.Distance( expected, Vector3.Transform( point, result.Rotation ) ) < 0.00001f );
	}

	[Fact]
	public void MovingTheTargetHandMovesThePropByTheSameAmount()
	{
		var hand = new Vector3( 3f, 4f, 5f );
		var first = RetargetedAttachment.Place( Vector3.One, Quaternion.Identity, Vector3.Zero, hand, 2f, false );
		var movement = new Vector3( 5f, -8f, 2f );
		var second = RetargetedAttachment.Place( Vector3.One, Quaternion.Identity, Vector3.Zero, hand + movement, 2f, false );
		Assert.Equal( movement, second.Position - first.Position );
	}
}
