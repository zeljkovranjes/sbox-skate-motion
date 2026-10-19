using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class InputRequestBufferTests
{
	[Fact]
	public void RepeatedActionPreservesFirstValueAndInsertionOrder()
	{
		var first = new InputCommandKey( 1, 2, 3, 4, 5, 6 );
		var second = new InputCommandKey( 1, 2, 3, 4, 5, 7 );
		var requests = new InputRequestBuffer( 2 );
		Assert.True( requests.Add( first, -.375f ) );
		Assert.True( requests.Add( second, 1f ) );
		Assert.False( requests.Add( first, 2.625f ) );
		Assert.Equal( 2, requests.Count );
		Assert.Equal( new InputRequest( first, -.375f ), requests[ 0 ] );
		Assert.Equal( new InputRequest( second, 1f ), requests[ 1 ] );
	}

	[Fact]
	public void ClearAllowsNewValuesOnTheNextUpdate()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( default, 0f );
		requests.Clear();
		Assert.Equal( 0, requests.Count );
		Assert.True( requests.Add( default, -1f ) );
		Assert.Equal( -1f, requests[ 0 ].Value );
	}

	[Fact]
	public void InsufficientCapacityReportsFailureWithoutDroppingExistingRequests()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( default, 1f );
		Assert.Throws<InvalidOperationException>( () => requests.Add( new( 1, 0, 0, 0, 0, 0 ), 2f ) );
		Assert.Equal( 1, requests.Count );
		Assert.Equal( 1f, requests[ 0 ].Value );
		Assert.Throws<ArgumentOutOfRangeException>( () => requests[ 1 ] );
	}
}
