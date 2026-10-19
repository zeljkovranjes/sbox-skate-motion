using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using System.Text;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ControllerActionRouterTests
{
	private static ControllerActionRouter Create()
	{
		var arc = new LimbArcTangent( new float[ 8 ], 1.5f, 3f );
		var objects = new ObjectMovementInputProcessor( arc, new( new float[ 16 ], new float[ 16 ] ),
			new( new float[ 8 ], new float[ 8 ] ), new( new float[ 8 ], new float[ 8 ] ), 1f / 6f, 6f, 1f / 3f );
		return new( arc, objects, new( .2f, .9f, 10f, new( .3f, .03f, .6f, .8f, 3f ),
			new( 2f, 3f, 1f, .5f, .5f, 1.5f, -1f, .01f, .5f, 2f, 2f, 1f, .25f, 3f ) ), 128 );
	}

	private static InputCommandKey Key( string name ) => InputCommandKey.Encode( Encoding.ASCII.GetBytes( name ) );

	private static int Index( ControllerActionRouter router, string name )
	{
		var key = Key( name );
		for ( var i = 0; i < router.Requests.Count; i++ )
		{
			if ( router.Requests[ i ].Key == key )
				return i;
		}
		return -1;
	}

	private static void Update( ControllerActionRouter router, ControllerReading reading,
		ControllerActionContext context = default, params QueuedInputCommand[] commands )
		=> router.Update( reading, .02f, context, commands, commands.Length );

	[Fact]
	public void HeldButtonsAdvanceStateWithoutRepeatingOffBoardEdges()
	{
		var router = Create();
		var reading = new ControllerReading( 0, 0, 0, 0, 0, 0, ControllerButtons.West );
		Update( router, reading );
		Assert.True( Index( router, "OB_JUMP" ) >= 0 );
		Assert.Equal( .02f, router.State.WestHeld );
		Update( router, reading );
		Assert.Equal( -1, Index( router, "OB_JUMP" ) );
		Assert.True( Index( router, "LEFTPUSH" ) >= 0 );
		Assert.Equal( .04f, router.State.WestHeld );
		Update( router, default );
		Assert.Equal( -1, Index( router, "LEFTPUSH" ) );
		Assert.Equal( 0f, router.State.WestHeld );
	}

	[Fact]
	public void SharedPushingRequestKeepsItsFirstInsertionBetweenFeet()
	{
		var router = Create();
		Update( router, new( 0, 0, 0, 0, 0, 0, ControllerButtons.West | ControllerButtons.South ) );
		Assert.True( Index( router, "RIGHTPUSH" ) < Index( router, "PUSHING" ) );
		Assert.True( Index( router, "PUSHING" ) < Index( router, "LEFTPUSH" ) );
		Assert.Equal( 1, Enumerable.Range( 0, router.Requests.Count ).Count( i => router.Requests[ i ].Key == Key( "PUSHING" ) ) );
	}

	[Fact]
	public void QueuedFlipsUseCurrentEffectivePushInsteadOfStaleContext()
	{
		var router = Create();
		var command = new QueuedInputCommand( 3, Key( "FRONTFLIP" ), .5f );
		var reading = new ControllerReading( 0, 0, 0, 0, .25f, 0, ControllerButtons.West );
		Update( router, reading, default, command );
		Assert.Equal( -1, Index( router, "FRONTFLIP" ) );
		var context = new ControllerActionContext
		{
			Buttons = new() { SuppressPush = true },
			Queue = new() { Pushing = true, HasAirGrab = false }
		};
		Update( router, reading, context, command );
		Assert.True( Index( router, "FRONTFLIP" ) >= 0 );
	}

	[Fact]
	public void EarlierAnalogRequestWinsWhenQueueUsesTheSameName()
	{
		var router = Create();
		Update( router, new( 0, 0, 0, .75f, 0, 0, 0 ), default, new QueuedInputCommand( 3, Key( "MANUAL" ), 1f ) );
		Assert.Equal( .75f, router.Requests[ Index( router, "MANUAL" ) ].Value );
		Assert.True( Index( router, "TRICK" ) > Index( router, "MANUAL" ) );
	}

	[Fact]
	public void HoldRequestClearsBeforeTheNextUpdateAndPrecedesQueue()
	{
		var router = Create();
		router.HoldPatternPending = true;
		Update( router, default, default, new QueuedInputCommand( 3, Key( "OLLIE" ), 1f ) );
		Assert.True( Index( router, "HOLDPATTERN" ) < Index( router, "TRICK" ) );
		Assert.False( router.HoldPatternPending );
		Update( router, default );
		Assert.Equal( -1, Index( router, "HOLDPATTERN" ) );
		Assert.Equal( -1, Index( router, "TRICK" ) );
	}

	[Fact]
	public void ContextualBumperSelectionCarriesAcrossRouterUpdates()
	{
		var router = Create();
		var reading = new ControllerReading( 0, 0, 0, 0, 0, 0, ControllerButtons.RightBumper );
		Update( router, reading );
		reading = reading with { Buttons = ControllerButtons.LeftBumper | ControllerButtons.RightBumper };
		Update( router, reading, new() { RetainPreviousBumper = true } );
		Assert.Equal( ControllerButtons.RightBumper, router.State.Current.Buttons );
		Update( router, reading );
		Assert.Equal( ControllerButtons.LeftBumper, router.State.Current.Buttons );
	}

	[Fact]
	public void FullTriggerEdgeProducesHandlingRequestOnlyOnce()
	{
		var router = Create();
		var reading = new ControllerReading( 0, 0, 0, 0, .75f, 0, 0 );
		Update( router, reading );
		Assert.Equal( -1, Index( router, "OB_DROPBOARD" ) );
		reading = reading with { LeftTrigger = 1f };
		Update( router, reading );
		Assert.True( Index( router, "OB_DROPBOARD" ) >= 0 );
		Assert.True( Index( router, "OB_RETRIEVEBOARD" ) >= 0 );
		Update( router, reading );
		Assert.Equal( -1, Index( router, "OB_DROPBOARD" ) );
		Assert.Equal( -1, Index( router, "OB_RETRIEVEBOARD" ) );
	}

	[Fact]
	public void ReusedRouterDoesNotAllocateDuringUpdates()
	{
		var router = Create();
		var reading = new ControllerReading( .25f, -.5f, .75f, -.25f, .5f, 1f, ControllerButtons.West );
		var commands = new[] { new QueuedInputCommand( 3, Key( "OLLIE" ), .5f ) };
		for ( var i = 0; i < 100; i++ )
			router.Update( reading, .02f, default, commands, 1 );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 100; i++ )
			router.Update( reading, .02f, default, commands, 1 );
		Assert.Equal( before, GC.GetAllocatedBytesForCurrentThread() );
	}
}
