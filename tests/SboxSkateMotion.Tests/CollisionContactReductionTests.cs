using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionContactReductionTests
{
	[Fact]
	public void MatchingPairRejectsNewestAndPreservesRecords()
	{
		var records = Pair();
		var before = records.ToArray();
		uint rejected = 7;
		Assert.True( CollisionContactReduction.RejectNewest( records, 2, Word( 0.01f ), false, ref rejected ) );
		Assert.Equal( 8u, rejected );
		Assert.Equal( before, records );
	}

	[Theory]
	[InlineData( 3 )]
	[InlineData( 7 )]
	public void BothBodyIdentitiesMustMatch( int offset )
	{
		var records = Pair();
		records[ 64 + offset ] = 2;
		uint rejected = 0;
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ), false, ref rejected ) );
		Assert.Equal( 0u, rejected );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 4 )]
	public void BothPointsMustBeStrictlyWithinSquaredTolerance( int offset )
	{
		var records = Pair();
		records[ 64 + offset ] = Word( 1 );
		uint rejected = 0;
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ), false, ref rejected ) );
		Assert.True( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ) + 1, false, ref rejected ) );
	}

	[Fact]
	public void NormalThresholdUsesWidenedRoundedDot()
	{
		var records = Pair();
		uint rejected = 0;
		records[ 74 ] = Word( 0.94f );
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ), false, ref rejected ) );
		records[ 74 ]++;
		Assert.True( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ), false, ref rejected ) );
	}

	[Fact]
	public void DisabledNegativeAndZeroToleranceRetainCandidate()
	{
		var records = Pair();
		uint rejected = 0;
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, Word( 1 ), true, ref rejected ) );
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, Word( -1 ), false, ref rejected ) );
		Assert.False( CollisionContactReduction.RejectNewest( records, 2, 0, false, ref rejected ) );
		Assert.False( CollisionContactReduction.RejectNewest( records, 1, Word( 1 ), false, ref rejected ) );
		Assert.Equal( 0u, rejected );
	}

	[Fact]
	public void CounterWrapsAndOnlyIncrementsOnce()
	{
		var records = new uint[ 192 ];
		for ( var i = 0; i < 3; ++i ) records[ i * 64 + 10 ] = Word( 1 );
		uint rejected = uint.MaxValue;
		Assert.True( CollisionContactReduction.RejectNewest( records, 3, Word( 1 ), false, ref rejected ) );
		Assert.Equal( 0u, rejected );
	}

	[Fact]
	public void InvalidCountFailsBeforeMutation()
	{
		var records = Pair();
		uint rejected = 8;
		Assert.Throws<ArgumentOutOfRangeException>( () => CollisionContactReduction.RejectNewest( records, 0, 0, false, ref rejected ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => CollisionContactReduction.RejectNewest( records, 3, 0, false, ref rejected ) );
		Assert.Equal( 8u, rejected );
	}

	private static uint[] Pair()
	{
		var records = new uint[ 128 ];
		records[ 10 ] = records[ 74 ] = Word( 1 );
		return records;
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
