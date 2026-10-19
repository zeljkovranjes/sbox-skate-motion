using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorWeightControllerTests
{
	[Fact]
	public void FeetAdvanceByOneRateStepPerInvocation()
	{
		var states = States( 1, 0f, .3f );
		var controller = Controller();
		for ( var i = 0; i < 4; i++ )
			Update( controller, states );
		Assert.Equal( 1f, states[ 0 ].Target );
		Assert.Equal( 1f, states[ 1 ].Target );
		Update( controller, states, disableFeet: true );
		Assert.Equal( .75f, states[ 0 ].Target );
		Assert.Equal( .3f, states[ 0 ].Override );
	}

	[Fact]
	public void DisabledFeetDoNotDisableTheHands()
	{
		var states = States( 1, .5f, 0f );
		Update( Controller(), states, disableFeet: true );
		Assert.Equal( .25f, states[ 0 ].Target );
		Assert.Equal( .75f, states[ 2 ].Target );
	}

	[Fact]
	public void PinCopiesItsWeightWithoutClampingAndFadesNormalTarget()
	{
		var states = States( 2, .5f, 0f );
		Controller().Update( states, new[] { 1.5f, -.5f, .6f, .7f }, new bool[ 4 ],
			new Vector3[ 2 ], Vector3.Zero, false );
		Assert.Equal( new EffectorWeightState( 2, .25f, 1.5f ), states[ 0 ] );
		Assert.Equal( -.5f, states[ 1 ].Override );
	}

	[Fact]
	public void ReleaseSignalForcesOneThenIndependentRatesFadeBothWeights()
	{
		var states = States( 3, .5f, .5f );
		var controller = Controller();
		controller.Update( states, new float[ 4 ], new[] { true, false, false, false }, new Vector3[ 2 ], Vector3.Zero, false );
		Assert.Equal( new EffectorWeightState( 3, .25f, 1f ), states[ 0 ] );
		Assert.Equal( new EffectorWeightState( 3, .25f, .375f ), states[ 1 ] );
		Update( controller, states );
		Assert.Equal( new EffectorWeightState( 3, 0f, .875f ), states[ 0 ] );
	}

	[Fact]
	public void InactiveHandRequiresStrictlyInsideTheOuterBox()
	{
		var states = States( 1, 0f, .2f );
		Controller().Update( states, new float[ 4 ], new bool[ 4 ],
			new[] { new Vector3( 2f, 0f, 0f ), new Vector3( 1.5f, 0f, 0f ) }, Vector3.Zero, false );
		Assert.Equal( 0f, states[ 2 ].Target );
		Assert.Equal( .25f, states[ 3 ].Target );
		Assert.Equal( .2f, states[ 2 ].Override );
	}

	[Fact]
	public void ActiveHandOutsideTheBoxFadesInsteadOfSticking()
	{
		var states = States( 1, .5f, 0f );
		Controller().Update( states, new float[ 4 ], new bool[ 4 ],
			new[] { new Vector3( 3f, 0f, 0f ), Vector3.Zero }, Vector3.Zero, false );
		Assert.Equal( .25f, states[ 2 ].Target );
		Assert.Equal( .75f, states[ 3 ].Target );
	}

	[Fact]
	public void HandTargetUsesTheSmallestAxisFadeAndAbsolutePosition()
	{
		var states = States( 1, 0f, 0f );
		var point = new Vector3( 1.25f, 1.5f, 1.75f );
		new EffectorWeightController( 1f, 1f, Vector3.One, Vector3.One ).Update(
			states, new float[ 4 ], new bool[ 4 ], new[] { point, -point }, new Vector3( 0f, 99f, 0f ), false );
		Assert.Equal( .25f, states[ 2 ].Target );
		Assert.Equal( .25f, states[ 3 ].Target );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 4 )]
	[InlineData( -1 )]
	public void UnhandledModesLeaveBothWeightsUnchanged( int mode )
	{
		var states = States( mode, .7f, .2f );
		Update( Controller(), states );
		foreach ( var state in states )
			Assert.Equal( new EffectorWeightState( mode, .7f, .2f ), state );
	}

	[Fact]
	public void ZeroRatesStillAllowAnExplicitOverrideAssignment()
	{
		var states = States( 2, .5f, 0f );
		new EffectorWeightController( 0f, 0f, Vector3.One, Vector3.One ).Update(
			states, new[] { .25f, .25f, .25f, .25f }, new bool[ 4 ], new Vector3[ 2 ], Vector3.Zero, false );
		Assert.Equal( new EffectorWeightState( 2, .5f, .25f ), states[ 0 ] );
	}

	[Fact]
	public void InvalidRigShapeIsRejected()
	{
		Assert.Throws<ArgumentException>( () => Controller().Update( new EffectorWeightState[ 3 ],
			new float[ 4 ], new bool[ 4 ], new Vector3[ 2 ], Vector3.Zero, false ) );
	}

	private static EffectorWeightController Controller() => new( .25f, .125f, Vector3.One, Vector3.One );

	private static EffectorWeightState[] States( int mode, float target, float pinned ) => new[]
	{
		new EffectorWeightState( mode, target, pinned ), new EffectorWeightState( mode, target, pinned ),
		new EffectorWeightState( mode, target, pinned ), new EffectorWeightState( mode, target, pinned )
	};

	private static void Update( EffectorWeightController controller, EffectorWeightState[] states, bool disableFeet = false ) =>
		controller.Update( states, new float[ 4 ], new bool[ 4 ], new Vector3[ 2 ], Vector3.Zero, disableFeet );
}
