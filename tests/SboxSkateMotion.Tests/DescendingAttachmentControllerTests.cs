using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DescendingAttachmentControllerTests
{
	[Theory]
	[InlineData( 0f, false )]
	[InlineData( 2f, false )]
	[InlineData( float.NaN, false )]
	[InlineData( -1f, true )]
	public void InactiveGatePreservesExistingRequests( float motion, bool blocked )
	{
		var attachment = new BoardAttachmentController();
		attachment.RequestFrame( At( 4f, 5f, 6f ) );
		attachment.RequestHeight( 7f, 9f );
		Create().Update( Input() with { VerticalMotion = motion, Blocked = blocked }, attachment );
		Assert.Equal( At( 4f, 7f, 6f ), attachment.Frame );
		Assert.Equal( 15f, attachment.FrameRemaining );
		Assert.Equal( 9f, attachment.HeightRemaining );
		Assert.True( attachment.FrameRequested );
		Assert.True( attachment.HeightRequested );
	}

	[Fact]
	public void NegativeZeroMotionDoesNotRequestAlignment()
	{
		var attachment = new BoardAttachmentController();
		Create().Update( Input() with { VerticalMotion = BitConverter.Int32BitsToSingle( int.MinValue ) }, attachment );
		Assert.False( attachment.FrameRequested );
		Assert.False( attachment.HeightRequested );
	}

	[Fact]
	public void FullResponseRequestsPlanarDisplacementAndReversedHeight()
	{
		var attachment = new BoardAttachmentController();
		Create().Update( Input() with { TargetWorld = At( 30f, 2f, 40f ), WorldDisplacement = new Vector3( 3f, 99f, 4f ) }, attachment );
		Assert.Equal( At( 3f, -2f, 4f ), attachment.Frame );
		Assert.True( attachment.FrameRequested );
		Assert.True( attachment.HeightRequested );
		Assert.Equal( 15f, attachment.FrameRemaining );
		Assert.Equal( 15f, attachment.HeightRemaining );
	}

	[Fact]
	public void TargetBelowBoardDoesNotLowerTheBlendedTarget()
	{
		var attachment = new BoardAttachmentController();
		Create().Update( Input() with { Board = At( 0f, 3f, 0f ), TargetWorld = At( 0f, 1f, 0f ) }, attachment );
		Assert.Equal( 0f, attachment.Frame.Translation.Y );
		Assert.True( attachment.HeightRequested );
	}

	[Fact]
	public void FrameConversionUsesRotationForDisplacementAndFullTransformForTarget()
	{
		var frame = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 100f, 10f, 200f ) );
		var attachment = new BoardAttachmentController();
		Create().Update( Input() with { WorldToModel = frame, Board = At( 1f, 2f, 3f ),
			TargetWorld = At( 2f, 3f, 4f ), WorldDisplacement = new Vector3( 4f, 5f, 6f ) }, attachment );
		Assert.Equal( frame.Rotation, attachment.Frame.Rotation );
		Assert.Equal( new Vector3( -2f, -11f, 6f ), attachment.Frame.Translation );
	}

	[Fact]
	public void EightPointCurveInterpolatesTheSelectedInterval()
	{
		var controller = Create( new[] { 0f, 1f, 0f, 1f, 0f, 1f, 0f, 1f } );
		var attachment = new BoardAttachmentController();
		controller.Update( Input() with { WorldDisplacement = new Vector3( 8f, 0f, 0f ), ResponseParameter = 2.5f }, attachment );
		Assert.Equal( 4f, attachment.Frame.Translation.X );
	}

	[Theory]
	[InlineData( -1f, 0f )]
	[InlineData( 8f, 8f )]
	public void ResponseUsesCurveEndpointsOutsideTheRange( float parameter, float expected )
	{
		var controller = Create( new[] { 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f } );
		var attachment = new BoardAttachmentController();
		controller.Update( Input() with { WorldDisplacement = new Vector3( 8f, 0f, 0f ), ResponseParameter = parameter }, attachment );
		Assert.Equal( expected, attachment.Frame.Translation.X );
		Assert.True( attachment.FrameRequested );
	}

	[Fact]
	public void ControllerOwnsItsCurveData()
	{
		var weights = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
		var controller = Create( weights );
		Array.Fill( weights, 0f );
		var attachment = new BoardAttachmentController();
		controller.Update( Input() with { WorldDisplacement = Vector3.UnitX }, attachment );
		Assert.Equal( 1f, attachment.Frame.Translation.X );
	}

	[Fact]
	public void InvalidCurveDataIsRejected()
	{
		Assert.Throws<ArgumentException>( () => new DescendingAttachmentController( Blender(), new float[ 7 ], new float[ 8 ] ) );
		Assert.Throws<ArgumentException>( () => new DescendingAttachmentController( Blender(), new[] { 0f, 1f, 3f, 2f, 4f, 5f, 6f, 7f }, new float[ 8 ] ) );
		Assert.Throws<ArgumentException>( () => Create( new[] { 0f, 0f, 0f, float.NaN, 0f, 0f, 0f, 0f } ) );
	}

	private static DescendingAttachmentController Create( float[] weights = null ) => new( Blender(),
		new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f }, weights ?? new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } );
	private static DescendingAttachmentInput Input() => new( -1f, false, LimbRigidTransform.Identity,
		LimbRigidTransform.Identity, LimbRigidTransform.Identity, Vector3.Zero, 0f );
	private static LimbRigidTransform At( float x, float y, float z ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, y, z ) };

	private static LimbPoseBlender Blender()
	{
		var angles = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		var arc = new LimbArcTangent( new[] { 1f / 17f, -1f / 15f, 1f / 13f, -1f / 11f, 1f / 9f, -1f / 7f, 1f / 5f, -1f / 3f }, MathF.PI / 2f, MathF.PI );
		return new LimbPoseBlender( angles, arc, .00001f, .1f );
	}
}
