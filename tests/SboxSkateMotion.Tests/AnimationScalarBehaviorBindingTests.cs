using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationScalarBehaviorBindingTests
{
	[Fact]
	public void PhaseAssignmentsShareOrderedOutputWithHeightAndReplaceAnimationPersistence()
	{
		var name = AnimationAttributeKey.Encode( "HEIGHT" );
		var output = new AnimationScalarOutput( new( 2, [] ), 8 );
		var scalar = new AnimationScalarBehaviorBinding( output );
		var definition = new AnimationScalarBehaviorDefinition( name, null, 1f, 0f, true );
		var binding = scalar.Bind( definition );
		Assert.Equal( -1, binding.Create() );
		binding.Enter( -1 );
		Assert.Equal( 0, output.QueuedCount );
		scalar.Update( definition );
		var height = new TrickHeightOutputSink( output, _ => name );
		height.Publish( 0, new( .75f, .15f ) );
		Assert.True( output.Attributes.Values[ 0 ].Persistent );
		scalar.Update( definition );
		Assert.False( output.Attributes.Values[ 0 ].Persistent );
		binding.Exit( -1 );
		binding.Transition();
		Assert.Equal( 4, output.QueuedCount );
		Assert.Equal( 1f, output.Queued[ 0 ].Value );
		Assert.Equal( .15f, output.Queued[ 1 ].Value );
		Assert.Equal( AnimationAttributeKey.Encode( "JUMPHEIGHTOVERRIDE" ), output.Queued[ 1 ].Name );
		Assert.Equal( 1f, output.Queued[ 2 ].Value );
		Assert.Equal( 0f, output.Queued[ 3 ].Value );
		Assert.Equal( 1, output.Attributes.Count );
		Assert.Equal( 0f, output.Attributes.Values[ 0 ].Attribute.Payload.Scalar );
		var assembly = new PhysicalAnimationAttributeAssembly( 4 );
		assembly.Gather( output.Queued, [] );
		Assert.Equal( 4, assembly.Count );
		Assert.Equal( .15f, assembly.Values[ 1 ].Value );
		Assert.Equal( (byte)6, assembly.Values[ 0 ].EventFlags );
		Assert.Equal( 4, output.QueuedCount );
		height.ResetQueued();
		Assert.Equal( 0, output.QueuedCount );
		Assert.Equal( 1, output.Attributes.Count );
	}

	[Fact]
	public void PhysicalModeEntryUsesCurrentCharacterAndChangesTheRidingDecision()
	{
		var first = PhysicalBoardOwnerFixture.State( false );
		var second = PhysicalBoardOwnerFixture.State( false );
		PhysicalCharacterState current = first;
		var binding = AnimationPhysicalModeBehavior.Bind( 2, () => current );
		Assert.Equal( -1, binding.Create() );
		binding.Enter( -1 );
		Assert.Equal( 2, first.PhysicsMode );
		Assert.True( PhysicalNonJumpMotionController.SelectRiding( first.PhysicsMode, 0f, 1f, 1UL << 54 ) );
		current = second;
		binding.Transition();
		Assert.Equal( 2, second.PhysicsMode );
		AnimationPhysicalModeBehavior.Bind( 1, () => second ).Enter( -1 );
		binding.Exit( -1 );
		AnimationPhysicalModeBehavior.Update();
		Assert.Equal( 1, second.PhysicsMode );
		Assert.False( PhysicalNonJumpMotionController.SelectRiding( second.PhysicsMode, 2f, 1f, 1UL << 54 ) );
		current = null;
		binding.Enter( -1 );
		Assert.Equal( 2, first.PhysicsMode );
	}
}
