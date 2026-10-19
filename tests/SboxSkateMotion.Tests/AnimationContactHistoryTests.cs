using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationContactHistoryTests
{
	private static AnimationContactMetadataTable Table()
	{
		var indices = new int[ 384 ];
		var kinds = new int[ 384 ];
		var keys = new AnimationMovementContactKey[ 384 ];
		for ( var i = 0; i < 384; i++ )
		{
			indices[ i ] = i;
			kinds[ i ] = 1000 + i;
			keys[ i ] = new( (uint)i, 2, 3, 4, 5 );
		}
		return new( indices, keys, kinds );
	}

	private static AnimationContactHistoryInput Input( int parent = 400, bool active = true )
		=> new( parent, active, false,
			new( Vector3.UnitX, Vector3.Zero, Vector3.Zero, Vector3.UnitZ, -Vector3.UnitZ,
				Vector3.UnitX, Vector3.Zero ), Vector3.UnitY, Vector3.UnitY, Vector3.Zero, Vector3.UnitX,
			Vector3.UnitX, Vector3.UnitY * .25f, Vector3.UnitZ, Vector3.UnitY,
			new( -1, 1, 3, .5f, 11, 13, default, default ) );

	[Fact]
	public void ChangedContactPublishesCurrentBeforeConfirmedMetadataAndFeedsClassification()
	{
		var history = new AnimationContactHistory();
		var table = Table();
		var input = Input();
		var first = history.Update( input, table );
		Assert.Equal( 49, first.SecondIndex );
		input = input with { Motion = new( 1f, 1f, 0f ) };
		var changed = history.Update( input, table );
		Assert.Equal( 49, changed.FirstIndex );
		Assert.Equal( 0, history.StableFrames );
		for ( var i = 0; i < 12; i++ )
		{
			var pending = history.Update( input, table );
			Assert.Equal( 1, pending.FirstIndex );
			Assert.Equal( 49, pending.SecondIndex );
		}
		var movement = new AnimationPhysicalMovementState( default );
		var classified = history.UpdateMovement( input, table, movement,
			new( new( 400, 400 ), 0, false, false, false, false, false, default ) );
		Assert.Equal( 13, history.StableFrames );
		Assert.Equal( 3, classified.Current );
		Assert.Equal( 1001, classified.Contact.Kind );
		Assert.Equal( new AnimationMovementContactKey( 1, 2, 3, 4, 5 ), classified.Contact.SecondKey );
		Assert.Equal( input.Contact.FirstFlags, classified.Contact.FirstFlags );
		Assert.Equal( input.Contact.Value, classified.Contact.Value );
	}

	[Fact]
	public void EntryUsesRetainedRollingFrameAndResetClearsCursorWithoutErasingStoredFrames()
	{
		var history = new AnimationContactHistory();
		var table = Table();
		for ( var i = 0; i < 61; i++ )
		{
			var rolling = Input( 100, false );
			history.Update( rolling with { Frame = rolling.Frame with { Position = Vector3.UnitX * i } }, table );
		}
		Assert.Equal( 31, history.FrameCursor );
		history.Update( Input(), table );
		Assert.Equal( Vector3.UnitX * 31, history.EntryFrame.Position );
		Assert.Equal( 32, history.FrameTickAt( 1 ) );
		var retained = history.FrameAt( 0 );
		history.Reset();
		Assert.Equal( 0, history.FrameCursor );
		Assert.Equal( retained, history.FrameAt( 0 ) );
		Assert.Equal( 4, history.FacingSide );
		Assert.Equal( 31, history.AwayFrames );
		var reset = Input( 300, false ) with { ResetFrames = true };
		history.Update( reset, table );
		Assert.Equal( reset.Frame, history.EntryFrame );
		Assert.Equal( 0, history.FrameCursor );
	}
}
