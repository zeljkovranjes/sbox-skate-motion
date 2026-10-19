using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationOwnerInputPhaseTests
{
	[Fact]
	public void EffectiveChannelAdvancesEachClockOnceAndPublishesBeforePhysicalInput()
	{
		foreach ( var enabled in new[] { false, true } )
		{
			var factory = new Factory();
			var overlay = CreateOverlay( factory );
			var owner = new AnimationBasePoseTreeOwner( factory, overlay.PublishBase );
			Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Play ) ) );
			var request = new AnimationChannelReplacement( 0, 1f, false, false, 0f, 0f, 0f,
				0x10000000u | (enabled ? 0x02000000u : 0u), false );
			Assert.Equal( AnimationOverlayRequestResult.Success,
				overlay.Request( new InputCommandKey( 1, 0, 0, 0, 0, 0 ), 0, true, request ) );
			var initial = AnimationPlaybackCompletion.Empty;
			factory.Base.Advance( .25f, 0f, ref initial );
			factory.Overlay.Advance( .1f, 0f, ref initial );
			var cache = new AnimationEvaluatedAttributeCache( 2 );
			var events = new List<string>();
			var phase = new AnimationOwnerInputPhase( owner, overlay,
				new AnimationEvaluatedAttributePublisher( cache, 2, 2 ),
				() =>
				{
					Assert.Equal( enabled ? 17f : 7f, cache.Values[ 0 ].Payload.Scalar );
					Assert.Equal( 1, owner.Completion.Flags );
					events.Add( "physical" );
				}, delta => { Assert.Equal( .3f, delta ); events.Add( "actor" ); } );
			var basePositions = new uint[] { 1057803469, 1062836634 };
			var overlayPositions = new uint[] { 1053609165, 1060320052 };
			var remaining = new uint[] { 1069128090, 1066611507 };
			for ( var step = 0; step < 2; step++ )
			{
				phase.Update( .3f, .75f );
				Assert.Equal( basePositions[ step ], BitConverter.SingleToUInt32Bits( factory.Base.Clock.Position ) );
				Assert.Equal( overlayPositions[ step ], BitConverter.SingleToUInt32Bits( factory.Overlay.Clock.Position ) );
				Assert.Equal( remaining[ step ], BitConverter.SingleToUInt32Bits( owner.Completion.Remaining ) );
				Assert.Equal( step == 0, owner.ReplacementPending );
				Assert.True( owner.ReplacementAdvanced );
				Assert.Equal( 1, overlay.Count );
			}
			Assert.Equal( new[] { "physical", "actor", "physical", "actor" }, events );
		}
	}

	[Fact]
	public void MissingBasePublicationBindingFailsBeforeClocksOrCallbacks()
	{
		var factory = new Factory();
		var overlay = CreateOverlay( factory );
		var owner = new AnimationBasePoseTreeOwner( factory );
		Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Play ) ) );
		var callbacks = 0;
		var phase = new AnimationOwnerInputPhase( owner, overlay,
			new AnimationEvaluatedAttributePublisher( new AnimationEvaluatedAttributeCache( 2 ), 2, 2 ),
			() => callbacks++, _ => callbacks++ );
		Assert.Throws<InvalidOperationException>( () => phase.Update( .3f, .75f ) );
		Assert.Equal( 0f, factory.Base.Clock.Position );
		Assert.Equal( 0, callbacks );
		Assert.False( owner.ReplacementAdvanced );
	}

	private static AnimationOverlayOwner CreateOverlay( Factory factory ) => new( 2, factory );

	private sealed class Factory : IAnimationBasePoseTreeFactory, IAnimationOverlayTreeFactory
	{
		internal readonly AnimationClipPlayback Base = Leaf( 7f );
		internal readonly AnimationClipPlayback Overlay = Leaf( 17f );
		public IAnimationPlaybackTree Create( int storage, int definition ) => new Tree( Base );
		public IAnimationPlaybackTree Create( int storage, AnimationChannelReplacement request ) => new Tree( Overlay );
		public void Publish( IAnimationPosePlaybackNode root ) { }
		public void Release( int storage, IAnimationPlaybackTree tree ) { }
	}

	private static AnimationClipPlayback Leaf( float value )
	{
		var metadata = new AnimationClipAttributeSet( "motion", new uint[ 6 ],
			[new( new( 1, 0, 0, 0, 0 ), .2f, .6f, 0,
				new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ) )] );
		var clip = new RawAnimationClip( "motion", 30f, 1f, 61, 0, 0,
			[], [], [], null, null, AnimationClipSamplingMode.Block, metadata );
		return new( clip, 0, new AnimationPoseSamplingContext( 1, false ) );
	}

	private sealed class Tree( IAnimationPlaybackNode root ) : IAnimationPlaybackTree
	{
		public IAnimationPlaybackNode Root => root;
	}
}
