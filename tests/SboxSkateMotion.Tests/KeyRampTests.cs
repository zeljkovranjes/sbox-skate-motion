using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class KeyRampTests
{
	[Fact]
	public void HeldKeyReachesFullOnlyAfterTheRiseTime()
	{
		var ramp = new KeyRamp( 0.35f, 0.15f );
		var value = 0f;
		for ( var frame = 0; frame < 10; frame++ )
			value = ramp.Step( 1f, 1f / 60f );
		Assert.InRange( value, 0.4f, 0.55f );
		for ( var frame = 0; frame < 20; frame++ )
			value = ramp.Step( 1f, 1f / 60f );
		Assert.Equal( 1f, value );
	}

	[Fact]
	public void ReleasedKeyCentresFasterThanItRose()
	{
		var ramp = new KeyRamp( 0.35f, 0.15f );
		for ( var frame = 0; frame < 30; frame++ )
			ramp.Step( 1f, 1f / 60f );
		// The fall time (0.15 s) is nine frames at 60 Hz; the tenth absorbs float rounding.
		for ( var frame = 0; frame < 10; frame++ )
			ramp.Step( 0f, 1f / 60f );
		Assert.Equal( 0f, ramp.Value );
	}

	[Fact]
	public void OppositeKeyCrossesCentreThenRampsTheOtherWay()
	{
		var ramp = new KeyRamp( 0.35f, 0.15f );
		for ( var frame = 0; frame < 30; frame++ )
			ramp.Step( 1f, 1f / 60f );
		var crossed = 0;
		while ( ramp.Value > 0f && crossed < 60 )
		{
			ramp.Step( -1f, 1f / 60f );
			crossed++;
		}
		Assert.InRange( crossed, 9, 10 );
		// Past centre it builds up at the slower rise rate.
		var before = ramp.Value;
		ramp.Step( -1f, 1f / 60f );
		Assert.Equal( -1f / 60f / 0.35f, ramp.Value - before, 4 );
	}
}
