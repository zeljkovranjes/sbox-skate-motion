using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCaptureEligibilityTests
{
	private static GrindCaptureInput Input => new( false, false, 1, 2, 1, Vector3.UnitY, Vector3.UnitX );

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SuppressionPreservesCaptureWithoutEvaluatingSelection( bool previous )
	{
		var capture = previous;
		Assert.False( Create().Evaluate( Input with { Suppressed = true, ProducerAccepted = true, SelectedIndex = 0 }, ref capture ) );
		Assert.Equal( previous, capture );
	}

	[Theory]
	[InlineData( false, 0, false, true )]
	[InlineData( true, 0, true, false )]
	[InlineData( true, 1, false, true )]
	[InlineData( true, -1, false, true )]
	public void CaptureRequiresAcceptedProducerAndFirstSelectedIndex( bool accepted, short index, bool expectedCapture, bool eligible )
	{
		var capture = true;
		Assert.Equal( eligible, Create().Evaluate( Input with { ProducerAccepted = accepted, SelectedIndex = index }, ref capture ) );
		Assert.Equal( expectedCapture, capture );
	}

	[Theory]
	[InlineData( 0, false )]
	[InlineData( 1, true )]
	[InlineData( 2, false )]
	[InlineData( 65535, false )]
	public void EligibilityRequiresExactlyOneUpdate( ushort count, bool expected )
	{
		var capture = false;
		Assert.Equal( expected, Create().Evaluate( Input with { UpdateCount = count }, ref capture ) );
	}

	[Theory]
	[InlineData( -32768, false )]
	[InlineData( -1, false )]
	[InlineData( 0, false )]
	[InlineData( 1, false )]
	[InlineData( 2, true )]
	[InlineData( 32767, true )]
	public void CandidateCountUsesSignedComparison( short count, bool expected )
	{
		var capture = false;
		Assert.Equal( expected, Create().Evaluate( Input with { CandidateCount = count }, ref capture ) );
	}

	[Fact]
	public void AngleEqualityPassesButNextLargerMinimumRejects()
	{
		var capture = false;
		Assert.True( Create( MathF.PI / 2 ).Evaluate( Input, ref capture ) );
		Assert.False( Create( MathF.BitIncrement( MathF.PI / 2 ) ).Evaluate( Input, ref capture ) );
	}

	[Fact]
	public void FailedEligibilityStillUpdatesCapture()
	{
		var capture = false;
		Assert.False( Create().Evaluate( Input with { UpdateCount = 2, CandidateCount = 0, ProducerAccepted = true, SelectedIndex = 0 }, ref capture ) );
		Assert.True( capture );
		Assert.False( Create().Evaluate( Input with { UpdateCount = 2 }, ref capture ) );
		Assert.False( capture );
	}

	[Fact]
	public void DegenerateDirectionUsesZeroAngleAndDoesNotRetainPriorResult()
	{
		var gate = Create();
		var capture = false;
		Assert.True( gate.Evaluate( Input, ref capture ) );
		Assert.False( gate.Evaluate( Input with { SelectedDirection = Vector3.Zero }, ref capture ) );
		Assert.True( Create( 0 ).Evaluate( Input with { ReferenceDirection = Vector3.Zero }, ref capture ) );
	}

	private static GrindCaptureEligibility Create( float minimum = .1f )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		return new( new( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ), minimum );
	}
}
