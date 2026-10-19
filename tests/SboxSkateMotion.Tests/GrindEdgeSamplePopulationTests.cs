using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeSamplePopulationTests
{
	[Fact]
	public void RejectedEdgesDoNotRenumberSuccessfulSourceIndices()
	{
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), [ Edge( 20f ), Edge(), Edge( -20f ) ], 3 );
		Assert.Equal( 3, queue.SourceEdgeCount );
		Assert.Equal( 1, queue.RemainingCount );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( -1f, ref output ) );
		Assert.Equal( 1, output.EdgeIndex );
		Assert.Equal( Vector3.Zero, output.Point );
	}

	[Fact]
	public void RebuildDiscardsPendingSamplesAndRestartsSourceIndices()
	{
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), [ Edge(), Edge( z: .25f ), Edge( z: .5f ) ], 3 );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( -1f, ref output ) );
		Assert.Equal( 2, queue.RemainingCount );
		queue.Populate( CreateSampler(), Motion(), [ Edge( z: .75f ) ], 1 );
		Assert.Equal( 1, queue.SourceEdgeCount );
		Assert.True( queue.Next( -1f, ref output ) );
		Assert.Equal( 0, output.EdgeIndex );
		Assert.Equal( .75f, output.Point.Z );
		Assert.False( queue.Next( -1f, ref output ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	public void EmptyOrRejectedRebuildPreservesLastSelectedOutput( int count )
	{
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), [ Edge(), Edge() ], 2 );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( -1f, ref output ) );
		var previous = output;
		queue.Populate( CreateSampler(), Motion(), [ Edge( 20f ) ], count );
		Assert.Equal( count, queue.SourceEdgeCount );
		Assert.Equal( 0, queue.RemainingCount );
		Assert.False( queue.Next( -1f, ref output ) );
		Assert.Equal( previous, output );
	}

	[Fact]
	public void PopulationUsesOnlyTheDeclaredInputPrefix()
	{
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), [ Edge( 20f ), Edge() ], 1 );
		Assert.Equal( 1, queue.SourceEdgeCount );
		Assert.Equal( 0, queue.RemainingCount );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 2 )]
	[InlineData( 41 )]
	public void InvalidPopulationCountPreservesPendingState( int count )
	{
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), [ Edge() ], 1 );
		Assert.Throws<ArgumentOutOfRangeException>( () => queue.Populate( CreateSampler(), Motion(), [ Edge() ], count ) );
		Assert.Equal( 1, queue.SourceEdgeCount );
		Assert.Equal( 1, queue.RemainingCount );
		var output = default( GrindEdgeSample );
		Assert.True( queue.Next( -1f, ref output ) );
		Assert.Equal( Vector3.Zero, output.Point );
	}

	[Fact]
	public void FullBufferRetainsInputOrderWhenSamplesTie()
	{
		var edges = new GrindEdgeRecord[ 40 ];
		Array.Fill( edges, Edge() );
		var queue = CreateQueue();
		queue.Populate( CreateSampler(), Motion(), edges, edges.Length );
		Assert.Equal( 40, queue.SourceEdgeCount );
		Assert.Equal( 40, queue.RemainingCount );
		var output = default( GrindEdgeSample );
		for ( var i = 0; i < edges.Length; i++ )
		{
			Assert.True( queue.Next( -1f, ref output ) );
			Assert.Equal( i, output.EdgeIndex );
		}
		Assert.False( queue.Next( -1f, ref output ) );
	}

	private static GrindEdgeTrajectory Motion() => new( Vector3.UnitY, Vector3.Zero, -Vector3.UnitY * 2f, 0f );

	private static GrindEdgeRecord Edge( float x = 0f, float z = 0f ) => new(
		new RailSegment( new Vector3( x - 4f, 0f, z ), new Vector3( x + 4f, 0f, z ) ), 123u, uint.MaxValue );

	private static GrindEdgeSampleQueue CreateQueue() => new(
		new GrindEdgeSelectionCurve( [ -4f, -3f, -2f, -1f, 0f, 1f, 2f, 3f ], new float[ 8 ] ),
		new GrindEdgeSelectionSettings( 100f, -100f, MathF.PI ) );

	private static GrindEdgeContactSampler CreateSampler()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f ],
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new( new LimbSignedAngle( math, 1e-8f, MathF.PI * 2f, 1f / (MathF.PI * 2f) ),
			new GrindEdgeSampleSettings( 2f, 1e-5f, .25f, 0f, 8f, MathF.PI ) );
	}
}
