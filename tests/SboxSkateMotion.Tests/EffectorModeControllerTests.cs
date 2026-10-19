using SkateMotion.Core.Animation;
using System.Numerics;
using Xunit;

namespace SkateMotion.Tests;

public class EffectorModeControllerTests
{
	[Fact]
	public void InactiveSlotsUseIndependentDefaultsAndClearWeights()
	{
		var states = Slots( new( 0, 0.7f, 0.6f ) );
		var offsets = Offsets();
		EffectorModeController.Update( states, new bool[ 4 ], new bool[ 4 ], new float[ 4 ], offsets, true, false, true );
		Assert.Equal( new[] { new EffectorWeightState( 1, 0f, 0f ), new( 1, 0f, 0f ), new( 0, 0f, 0f ), new( 1, 0f, 0f ) }, states );
		Assert.Equal( Offsets(), offsets );
	}

	[Fact]
	public void PinTakesPriorityOverReleaseAndPreservesOffsets()
	{
		var states = new[] { new EffectorWeightState( 0, 0.7f, 0.6f ), new( 1, 0.7f, 0.6f ), new( 0, 0f, 0f ), new( 1, 0f, 0f ) };
		var offsets = Offsets();
		var pins = new[] { true, true, true, true };
		var releases = new[] { true, true, true, true };
		EffectorModeController.Update( states, pins, releases, new[] { 0.2f, 0.4f, 1.5f, -0.3f }, offsets, false, false, false );
		Assert.Equal( new[] { new EffectorWeightState( 2, 0f, 0.2f ), new( 2, 0.7f, 0.4f ), new( 2, 0f, 1.5f ), new( 2, 0f, -0.3f ) }, states );
		Assert.Equal( Offsets(), offsets );
		Assert.All( pins, Assert.True );
		Assert.All( releases, Assert.True );
	}

	[Fact]
	public void EnteringReleaseClearsOffsetsAndKeepsOnlyActiveTargetWeight()
	{
		var states = new[] { new EffectorWeightState( 0, 0.7f, 0.6f ), new( 1, 0.7f, 0.6f ), new( 0, 0.8f, 0f ), new( 1, 0.8f, 0f ) };
		var offsets = Offsets();
		EffectorModeController.Update( states, new bool[ 4 ], new[] { true, true, true, true }, new[] { 0.2f, 0.3f, 0.4f, 0.5f }, offsets, true, true, true );
		Assert.Equal( new[] { new EffectorWeightState( 3, 0f, 0.2f ), new( 3, 0.7f, 0.3f ), new( 3, 0f, 0.4f ), new( 3, 0.8f, 0.5f ) }, states );
		Assert.All( offsets, v => Assert.Equal( Vector3.Zero, v ) );
	}

	[Fact]
	public void ActiveModeReturnsToDefaultOnlyAtExactZero()
	{
		var states = new[] { new EffectorWeightState( 1, -0f, 0.6f ), new( 1, 1e-20f, 0.6f ), new( 1, -1e-20f, 0.6f ), new( 1, float.NaN, 0.6f ) };
		var offsets = Offsets();
		EffectorModeController.Update( states, new bool[ 4 ], new bool[ 4 ], new float[ 4 ], offsets, false, false, false );
		Assert.Equal( new EffectorWeightState( 0, -0f, 0f ), states[ 0 ] );
		Assert.Equal( int.MinValue, BitConverter.SingleToInt32Bits( states[ 0 ].Target ) );
		Assert.Equal( new EffectorWeightState( 1, 1e-20f, 0.6f ), states[ 1 ] );
		Assert.Equal( new EffectorWeightState( 1, -1e-20f, 0.6f ), states[ 2 ] );
		Assert.Equal( 1, states[ 3 ].Mode );
		Assert.True( float.IsNaN( states[ 3 ].Target ) );
		Assert.Equal( 0.6f, states[ 3 ].Override );
		Assert.Equal( Offsets(), offsets );
	}

	[Fact]
	public void RemovingPinEntersReleaseWithoutResettingWeightsOrOffsets()
	{
		var states = Slots( new( 2, 0.7f, 0f ) );
		var offsets = Offsets();
		EffectorModeController.Update( states, new[] { false, true, false, true }, new[] { false, false, true, true }, new[] { 0.2f, 0.3f, 0.4f, 0.5f }, offsets, false, false, false );
		Assert.Equal( new[] { new EffectorWeightState( 3, 0.7f, 0f ), new( 2, 0.7f, 0f ), new( 3, 0.7f, 0f ), new( 2, 0.7f, 0f ) }, states );
		Assert.Equal( Offsets(), offsets );
	}

	[Fact]
	public void ReleasingSlotsRepinBeforeCheckingCompletion()
	{
		var states = Slots( new( 3, 0.7f, 0.6f ) );
		var offsets = Offsets();
		EffectorModeController.Update( states, new[] { true, true, true, true }, new[] { false, false, true, true }, new[] { 0f, 0.3f, 0f, 0.5f }, offsets, true, false, false );
		Assert.Equal( new[] { new EffectorWeightState( 1, 0.7f, 0f ), new( 2, 0.7f, 0.3f ), new( 2, 0.7f, 0f ), new( 2, 0.7f, 0.5f ) }, states );
		Assert.Equal( Vector3.Zero, offsets[ 0 ] );
		Assert.Equal( Offsets()[ 1.. ], offsets[ 1.. ] );
	}

	[Fact]
	public void ReleaseCompletionPreservesSignedZeroAndIgnoresNonzeroWeights()
	{
		var states = new[] { new EffectorWeightState( 3, 0.7f, -0f ), new( 3, 0.7f, 1e-20f ), new( 3, 0.7f, -1e-20f ), new( 3, 0.7f, float.NaN ) };
		var offsets = Offsets();
		EffectorModeController.Update( states, new bool[ 4 ], new bool[ 4 ], new float[ 4 ], offsets, false, false, false );
		Assert.Equal( 0, states[ 0 ].Mode );
		Assert.Equal( 0.7f, states[ 0 ].Target );
		Assert.Equal( int.MinValue, BitConverter.SingleToInt32Bits( states[ 0 ].Override ) );
		Assert.Equal( Vector3.Zero, offsets[ 0 ] );
		Assert.All( states[ 1.. ], v => Assert.Equal( 3, v.Mode ) );
		Assert.Equal( Offsets()[ 1.. ], offsets[ 1.. ] );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 4 )]
	[InlineData( int.MaxValue )]
	public void UnknownModesPreserveState( int mode )
	{
		var states = Slots( new( mode, 0.7f, 0.6f ) );
		var offsets = Offsets();
		EffectorModeController.Update( states, new[] { true, false, true, false }, new[] { false, true, true, false }, new float[ 4 ], offsets, true, true, true );
		Assert.Equal( Slots( new( mode, 0.7f, 0.6f ) ), states );
		Assert.Equal( Offsets(), offsets );
	}

	[Fact]
	public void ReleaseCompletesOnTheInvocationAfterItsWeightReachesZero()
	{
		var states = Slots( new( 2, 0.5f, 0.5f ) );
		var offsets = Offsets();
		var flags = new bool[ 4 ];
		var pins = new float[ 4 ];
		var weights = new EffectorWeightController( 0.25f, 0.25f, Vector3.One, Vector3.One );
		void Tick()
		{
			EffectorModeController.Update( states, flags, flags, pins, offsets, true, false, true );
			weights.Update( states, pins, flags, new Vector3[ 2 ], Vector3.Zero, false );
		}
		Tick();
		Assert.All( states, v => Assert.Equal( new EffectorWeightState( 3, 0.25f, 0.25f ), v ) );
		Tick();
		Assert.All( states, v => Assert.Equal( new EffectorWeightState( 3, 0f, 0f ), v ) );
		Assert.Equal( Offsets(), offsets );
		Tick();
		Assert.Equal( new[] { new EffectorWeightState( 1, 0.25f, 0f ), new( 1, 0.25f, 0f ), new( 0, 0f, 0f ), new( 1, 0.25f, 0f ) }, states );
		Assert.All( offsets, v => Assert.Equal( Vector3.Zero, v ) );
	}

	[Fact]
	public void InvalidSlotCountIsRejectedBeforeMutation()
	{
		var states = Slots( new( 0, 0.7f, 0.6f ) );
		Assert.Throws<ArgumentException>( () => EffectorModeController.Update( states, new bool[ 3 ], new bool[ 4 ], new float[ 4 ], Offsets(), true, true, true ) );
		Assert.Equal( Slots( new( 0, 0.7f, 0.6f ) ), states );
	}

	private static EffectorWeightState[] Slots( EffectorWeightState state ) => new[] { state, state, state, state };

	private static Vector3[] Offsets() => new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.One };
}
