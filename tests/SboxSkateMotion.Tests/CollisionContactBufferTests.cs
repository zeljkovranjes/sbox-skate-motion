using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionContactBufferTests
{
	[Fact]
	public void CollectorStreamsMoreThanOneInlineBatchWithoutLosingRecords()
	{
		var buffer = new CollisionContactBuffer();
		var state = new uint[ 0x3290 / 4 ];
		state[ 2 ] = 120;
		state[ 4 ] = BitConverter.SingleToUInt32Bits( -1f );
		state[ 0x328c / 4 ] = 0x01000000;
		var collector = new CollisionContactCollector( state, buffer );
		for ( var i = 0; i < 120; ++i )
		{
			var record = collector.AllocateContact();
			Assert.Equal( 64, record.Length );
			for ( var word = 0; word < record.Length; ++word ) record[ word ] = (uint)(i * 64 + word);
			collector.CompleteContact();
		}
		collector.Flush();
		Assert.Equal( 120, buffer.Count );
		Assert.Equal( Enumerable.Range( 0, 120 * 64 ).Select( i => (uint)i ), buffer.Records.ToArray() );
	}

	[Fact]
	public void ResetReusesStorageWithoutPublishingOldContacts()
	{
		var buffer = new CollisionContactBuffer();
		buffer.Write( Enumerable.Repeat( 12u, 100 * 64 ).ToArray() );
		buffer.Reset();
		Assert.True( buffer.Records.IsEmpty );
		Assert.Throws<ArgumentException>( () => buffer.Write( new uint[ 63 ] ) );
		Assert.Equal( 0, buffer.Count );
		var record = Enumerable.Repeat( 27u, 64 ).ToArray();
		buffer.Write( record );
		Assert.Equal( 1, buffer.Count );
		Assert.Equal( record, buffer.Records.ToArray() );
	}
}
