using System.Text;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DropInControlsTests
{
	private static ControllerActionRouter Create()
	{
		var arc = new LimbArcTangent( new float[ 8 ], 1.5f, 3f );
		var objects = new ObjectMovementInputProcessor( arc, new( new float[ 16 ], new float[ 16 ] ),
			new( new float[ 8 ], new float[ 8 ] ), new( new float[ 8 ], new float[ 8 ] ), 1f / 6f, 6f, 1f / 3f );
		return new( arc, objects, new( .2f, .9f, 10f, new( .3f, .03f, .6f, .8f, 3f ),
			new( 2f, 3f, 1f, .5f, .5f, 1.5f, -1f, .01f, .5f, 2f, 2f, 1f, .25f, 3f ) ), 128 );
	}

	private static bool Has( ControllerActionRouter router, string name )
	{
		var key = InputCommandKey.Encode( Encoding.ASCII.GetBytes( name ) );
		for ( var i = 0; i < router.Requests.Count; i++ )
		{
			if ( router.Requests[ i ].Key == key )
				return true;
		}
		return false;
	}

	[Fact]
	public void HoldingTheMountButtonOnFootWaitsAtTheLipAndLettingGoGoesOver()
	{
		// On foot with the board in hand, the mount button (Y) starts the drop-in and, held, keeps the rider waiting at the
		// lip while the drop-in runs (an interaction, so it is not a new press again); let go, it is no longer held.
		var router = Create();
		var onFoot = new ControllerActionContext { Buttons = new() { SuppressPush = true, SuppressBrake = true } };
		var waiting = onFoot with { Buttons = onFoot.Buttons with { SuppressNewToggle = true } };
		var held = new ControllerReading( 0, 0, 0, 0, 0, 0, ControllerButtons.North );
		router.Update( held, .02f, onFoot, System.Array.Empty<QueuedInputCommand>(), 0 );
		Assert.True( Has( router, "NEWTOGGLEOFFBOARDSTATE" ) );
		Assert.True( Has( router, "TOGGLEOFFBOARDSTATE" ) );
		for ( var frame = 0; frame < 60; frame++ )
		{
			router.Update( held, .02f, waiting, System.Array.Empty<QueuedInputCommand>(), 0 );
			Assert.True( Has( router, "TOGGLEOFFBOARDSTATE" ) );
			Assert.False( Has( router, "NEWTOGGLEOFFBOARDSTATE" ) );
		}
		router.Update( default, .02f, waiting, System.Array.Empty<QueuedInputCommand>(), 0 );
		Assert.False( Has( router, "TOGGLEOFFBOARDSTATE" ) );
	}

	[Fact]
	public void TheGraphWaitsWhileTheMountIsHeldAndGoesOverOnceItIsLetGo()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		var atLip = new RiderAnimationInput { Activity = RiderActivity.Mounting, HoldingBoard = true, EdgeDistance = 0.3f };
		source.Fill( facts, atLip with { MountHeld = true }, false, 1f / 60f );
		Assert.True( facts.Intents.ContainsKey( "ob-mount-raw" ) );
		source.Fill( facts, atLip, false, 1f / 60f );
		Assert.False( facts.Intents.ContainsKey( "ob-mount-raw" ) );
		Assert.True( facts.Intents.ContainsKey( "ob-mount" ) );
	}
}
