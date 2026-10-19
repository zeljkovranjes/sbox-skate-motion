using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using System.Text;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ControllerFrameProcessorTests
{
	private static (ControllerActionRouter Router, ControllerFrameProcessor Frame) Create()
	{
		var arc = new LimbArcTangent( new float[ 8 ], 1.5f, 3f );
		var objects = new ObjectMovementInputProcessor( arc, new( new float[ 16 ], new float[ 16 ] ),
			new( new float[ 8 ], new float[ 8 ] ), new( new float[ 8 ], new float[ 8 ] ), 1f / 6f, 6f, 1f / 3f );
		var router = new ControllerActionRouter( arc, objects,
			new( .2f, .9f, 10f, new( .3f, .03f, .6f, .8f, 3f ),
				new( 2f, 3f, 1f, .5f, .5f, 1.5f, -1f, .01f, .5f, 2f, 2f, 1f, .25f, 3f ) ), 128 );
		return (router, new( router ));
	}

	private static InputCommandKey Key( string name ) => InputCommandKey.Encode( Encoding.ASCII.GetBytes( name ) );

	private static float? Value( ControllerFrameProcessor frame, string name )
	{
		for ( var i = 0; i < frame.Requests.Count; i++ )
		{
			if ( frame.Requests[ i ].Key == Key( name ) )
			{
				return frame.Requests[ i ].Value;
			}
		}
		return null;
	}

	[Fact]
	public void QueueIsConsumedAndOldRequestsAreClearedOnTheNextFrame()
	{
		var (_, frame) = Create();
		var commands = new[] { new QueuedInputCommand( 3, Key( "CUSTOM" ), .5f ) };
		var count = 1;
		frame.Update( default, .02f, default, default, commands, ref count, null );
		Assert.Equal( 0, count );
		Assert.Equal( 1f, Value( frame, "CUSTOM" ) );
		frame.Update( default, .02f, default, default, commands, ref count, null );
		Assert.Null( Value( frame, "CUSTOM" ) );
		Assert.Null( Value( frame, "TRICK" ) );
	}

	[Fact]
	public void BrakeGatePreservesSamplesTimersAndHoldWhileDiscardingCommands()
	{
		var (router, frame) = Create();
		router.HoldPatternPending = true;
		var commands = new[] { new QueuedInputCommand( 3, Key( "CUSTOM" ), .5f ) };
		var count = 1;
		frame.Update( new( 0, 0, 0, 0, 0, 0, ControllerButtons.West ), .5f, default,
			new() { ForceBrake = true, BlockControllerDuringForcedBrake = true }, commands, ref count, null );
		Assert.Equal( 0, count );
		Assert.Equal( default, router.State.Current );
		Assert.Equal( 0f, router.State.WestHeld );
		Assert.True( router.HoldPatternPending );
		Assert.Equal( 1, frame.Requests.Count );
		Assert.Equal( 1f, Value( frame, "FORCEBRAKE" ) );
	}

	[Fact]
	public void ForceBrakeAloneDoesNotSuppressControllerSampling()
	{
		var (router, frame) = Create();
		var count = 0;
		frame.Update( new( 0, 0, 0, 0, 0, 0, ControllerButtons.West ), .02f, default,
			new() { ForceBrake = true }, Array.Empty<QueuedInputCommand>(), ref count, null );
		Assert.Equal( .02f, router.State.WestHeld );
		Assert.Equal( 1f, Value( frame, "OB_JUMP" ) );
		Assert.Equal( 1f, Value( frame, "FORCEBRAKE" ) );
	}

	[Fact]
	public void EmptyOverrideStillBypassesControllerAndConsumesQueue()
	{
		var (router, frame) = Create();
		router.HoldPatternPending = true;
		var commands = new[] { new QueuedInputCommand( 3, Key( "CUSTOM" ), .5f ) };
		var count = 1;
		frame.Update( new( 0, 0, 0, 0, 0, 0, ControllerButtons.West ), .02f, default,
			new() { HasOverride = true }, commands, ref count, new( 0 ) );
		Assert.Equal( 0, count );
		Assert.Equal( 0, frame.Requests.Count );
		Assert.Equal( 0f, router.State.WestHeld );
		Assert.True( router.HoldPatternPending );
	}

	[Fact]
	public void SuppressedForcedBailIsConsumedInsteadOfDelayed()
	{
		var (_, frame) = Create();
		frame.BailPending = true;
		var count = 0;
		frame.Update( default, .02f, default, new() { SuppressForcedBail = true },
			Array.Empty<QueuedInputCommand>(), ref count, null );
		Assert.False( frame.BailPending );
		Assert.Null( Value( frame, "WIPEOUTREQUEST" ) );
		frame.Update( default, .02f, default, default, Array.Empty<QueuedInputCommand>(), ref count, null );
		Assert.Null( Value( frame, "WIPEOUTREQUEST" ) );
	}

	[Fact]
	public void ForcedRequestsReplaceExistingValuesWithoutChangingTheirPositions()
	{
		var (_, frame) = Create();
		var overrides = new InputRequestBuffer( 3 );
		overrides.Add( Key( "FORCEBRAKE" ), .25f );
		overrides.Add( Key( "CUSTOM" ), .75f );
		overrides.Add( Key( "WIPEOUTREQUEST" ), -.5f );
		frame.BailPending = true;
		var count = 0;
		frame.Update( default, .02f, default, new() { HasOverride = true, ForceBrake = true },
			Array.Empty<QueuedInputCommand>(), ref count, overrides );
		Assert.False( frame.BailPending );
		Assert.Equal( 3, frame.Requests.Count );
		Assert.Equal( new InputRequest( Key( "FORCEBRAKE" ), 1f ), frame.Requests[ 0 ] );
		Assert.Equal( new InputRequest( Key( "CUSTOM" ), .75f ), frame.Requests[ 1 ] );
		Assert.Equal( new InputRequest( Key( "WIPEOUTREQUEST" ), 1f ), frame.Requests[ 2 ] );
		Assert.Equal( .25f, overrides[ 0 ].Value );
	}

	[Fact]
	public void ResumingUsesTheLastSampledButtonState()
	{
		var (router, frame) = Create();
		var count = 0;
		var commands = Array.Empty<QueuedInputCommand>();
		var held = new ControllerReading( 0, 0, 0, 0, 0, 0, ControllerButtons.West );
		frame.Update( held, .02f, default, default, commands, ref count, null );
		frame.Update( default, .5f, default, new() { HasOverride = true }, commands, ref count, new( 0 ) );
		frame.Update( held, .02f, default, default, commands, ref count, null );
		Assert.Null( Value( frame, "OB_JUMP" ) );
		Assert.Equal( .04f, router.State.WestHeld );
	}

	[Fact]
	public void AliasedOverrideStorageIsRejectedBeforeClearingRequests()
	{
		var (_, frame) = Create();
		frame.Requests.Add( Key( "CUSTOM" ), .5f );
		var count = 0;
		Assert.Throws<ArgumentException>( () => frame.Update( default, .02f, default,
			new() { HasOverride = true }, Array.Empty<QueuedInputCommand>(), ref count, frame.Requests ) );
		Assert.Equal( .5f, Value( frame, "CUSTOM" ) );
	}
}
