using SkateMotion.Core.Input;
using System.Text;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class QueuedInputProcessorTests
{
	private static InputCommandKey Key( string name ) => InputCommandKey.Encode( Encoding.ASCII.GetBytes( name ) );

	private static QueuedInputActions Evaluate( string name, QueuedInputContext context = default, int kind = 3 )
		=> QueuedInputProcessor.Evaluate( new( kind, Key( name ), -.75f ), context );

	[Fact]
	public void KeyIsCaseInsensitiveAndStopsAtNull()
	{
		Assert.Equal( Key( "ollie" ), Key( "OLLIE" ) );
		Assert.Equal( Key( "OLLIE" ), Key( "OLLIE\0ignored" ) );
	}

	[Fact]
	public void KeyUsesSixFixedWidthWordsAndTruncatesAfterThirtySixBytes()
	{
		Assert.Equal( new InputCommandKey( 79235168, 0, 0, 0, 0, 0 ), Key( "0" ) );
		Assert.Equal( new InputCommandKey( 0, 0, 0, 0, 0, 0 ), Key( "" ) );
		Assert.NotEqual( Key( new string( 'A', 30 ) ), Key( new string( 'A', 31 ) ) );
		Assert.Equal( Key( new string( 'A', 36 ) ), Key( new string( 'A', 36 ) + "B" ) );
	}

	[Fact]
	public void KeyPreservesSignedByteArithmetic()
	{
		Assert.Equal( unchecked( (uint)-175 * 79235168u ), InputCommandKey.Encode( new byte[] { 128 } ).A );
	}

	[Fact]
	public void UnknownStringCommandsRemainTricksWithoutRestrictions()
	{
		var result = Evaluate( "CUSTOM" );
		Assert.True( result.HasCommand );
		Assert.True( result.IsTrick );
		Assert.Equal( Key( "CUSTOM" ), result.Key );
		Assert.Equal( -.75f, result.Speed );
	}

	[Fact]
	public void DirectCommandsBypassTrickRestrictions()
	{
		var context = new QueuedInputContext( true, true, true, true, true, true, false, true );
		var result = Evaluate( "FRONTFLIP", context, 2 );
		Assert.True( result.HasCommand );
		Assert.False( result.IsTrick );
		Assert.Equal( 0f, result.Speed );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 4 )]
	public void UnrecognizedRecordKindsProduceNoRequest( int kind )
	{
		Assert.False( Evaluate( "OLLIE", kind: kind ).HasCommand );
	}

	[Fact]
	public void FingerRestrictionDoesNotSuppressOtherTricks()
	{
		var context = new QueuedInputContext { SuppressFingerCommands = true };
		Assert.False( Evaluate( "FINGERFLIP", context ).HasCommand );
		Assert.False( Evaluate( "FS_VARIAL", context ).HasCommand );
		Assert.False( Evaluate( "BS_VARIAL", context ).HasCommand );
		Assert.True( Evaluate( "KICKFLIP", context ).HasCommand );
	}

	[Fact]
	public void LateRestrictionCanBlockNamedLateTricksOrAllTricks()
	{
		var context = new QueuedInputContext { SuppressLateCommands = true };
		Assert.False( Evaluate( "L_FS_SHUVIT", context ).HasCommand );
		Assert.True( Evaluate( "OLLIE", context ).HasCommand );
		Assert.False( Evaluate( "OLLIE", context with { BlockAllDuringLateRestriction = true } ).HasCommand );
		Assert.True( Evaluate( "OLLIE", new() { BlockAllDuringLateRestriction = true } ).HasCommand );
	}

	[Theory]
	[InlineData( "FRONTFLIP" )]
	[InlineData( "BACKFLIP" )]
	public void FlipsRequireGrabWithoutPushingAndRespectTheirRestriction( string name )
	{
		Assert.False( Evaluate( name ).HasCommand );
		var context = new QueuedInputContext { HasAirGrab = true };
		Assert.True( Evaluate( name, context ).HasCommand );
		Assert.False( Evaluate( name, context with { Pushing = true } ).HasCommand );
		Assert.False( Evaluate( name, context with { SuppressFlips = true } ).HasCommand );
	}

	[Fact]
	public void SlideRestrictionLeavesOtherRequestsAvailable()
	{
		var context = new QueuedInputContext { SuppressSlides = true };
		Assert.False( Evaluate( "SLIDEFS180", context ).HasCommand );
		Assert.False( Evaluate( "SLIDEBS180", context ).HasCommand );
		Assert.True( Evaluate( "POPSHUVIT", context ).HasCommand );
	}

	[Fact]
	public void BasicRestrictionAcceptsBothFullShuvitVariantsAndRejectsUnknowns()
	{
		var context = new QueuedInputContext { RestrictToBasicTricks = true };
		Assert.True( Evaluate( "360POPSHUVIT", context ).HasCommand );
		Assert.True( Evaluate( "FS360POPSHUVIT", context ).HasCommand );
		Assert.True( Evaluate( "INWARDHEELFLIP", context ).HasCommand );
		Assert.False( Evaluate( "CUSTOM", context ).HasCommand );
		Assert.False( Evaluate( "FINGERFLIP", context ).HasCommand );
	}

	[Fact]
	public void HoldPatternRequestIsConsumedExactlyOnce()
	{
		var pending = true;
		Assert.True( QueuedInputProcessor.ConsumeHoldPattern( ref pending ) );
		Assert.False( pending );
		Assert.False( QueuedInputProcessor.ConsumeHoldPattern( ref pending ) );
		pending = true;
		Assert.True( QueuedInputProcessor.ConsumeHoldPattern( ref pending ) );
	}
}
