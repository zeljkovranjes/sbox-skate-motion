using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingOrientationTests
{
	private static readonly LimbArcTangent Angles = new( new float[ 8 ], MathF.PI / 2f, MathF.PI );
	private static readonly LimbRigidTransform Frame = new(
		new( new Vector3( 0, 0, -1 ), Vector3.UnitY, Vector3.UnitX ), new( 4, 2, 7 ) );

	[Fact]
	public void UsesCompletedPhysicalFrameAndPreservesRequestFields()
	{
		var input = new BoardHandlingInput( false, true, false, true, true, false, false, 99f, 99f, true );
		var result = BoardHandlingOrientation.Publish( input, Frame, new( 4.5f, 3, 7.2f ),
			new( 5, 1, 4 ), 0, new( 1, 4, 9 ), Angles );
		Assert.Equal( input with { YawRadians = result.YawRadians, PitchRadians = 0f }, result );
		Assert.Equal( MathF.PI / 2f - 1f / 3f, result.YawRadians, 6 );
		Assert.Equal( 0u, BitConverter.SingleToUInt32Bits( result.PitchRadians ) );
	}

	[Fact]
	public void RetrievalStateUsesRetainedPointAndProjectsAlongPhysicalUp()
	{
		var frame = new LimbRigidTransform( new( Vector3.UnitX, new( 1, 1, 0 ), Vector3.UnitZ ), Vector3.Zero );
		var result = BoardHandlingOrientation.Publish( default, frame, new( 0, 2, 0 ),
			new( 100, 0, 0 ), 3, new( 3, 2, 1 ), Angles );
		Assert.Equal( 1f, result.YawRadians );
		Assert.Equal( 0f, result.PitchRadians );
	}
}
