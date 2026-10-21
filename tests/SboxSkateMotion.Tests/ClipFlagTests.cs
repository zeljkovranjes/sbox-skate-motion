using System.Linq;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipFlagTests
{
	[Theory]
	[InlineData( "canbranch" )]
	[InlineData( "cantranstounderflip" )]
	[InlineData( "jbr_airdismount_to_big_dn_0" )]
	public void AnAttributeKeyDecodesToItsName( string name )
	{
		Assert.Equal( name, AnimationAttributeKey.Encode( name ).Decode() );
	}

	[Theory]
	// An ollie's branch window opens partway through and lasts to its end; an underflip window is a slice of a flip.
	[InlineData( "can-branch", 0.3f, false )]
	[InlineData( "can-branch", 0.5f, true )]
	[InlineData( "can-branch", 1f, true )]
	[InlineData( "cantranstounderflip", 0.25f, true )]
	[InlineData( "cantranstounderflip", 0.4f, false )]
	// A window marked -1 lasts the whole clip; a flag the clip does not carry is shut.
	[InlineData( "one-foot-early-out", 0.9f, true )]
	[InlineData( "midout", 0.1f, false )]
	public void AClipsFlagFollowsItsOwnWindow( string flag, float phase, bool open )
	{
		Assert.Equal( open, ClipFlags.Active( Set(), flag, phase ) );
	}

	[Fact]
	public void WithoutAttributesAFlagIsUnknown()
	{
		Assert.Null( ClipFlags.Active( (AnimationClipAttributeSet)null, "can-branch", 0.5f ) );
		Assert.False( ClipFlags.IsTimed( "absorb-land" ) );
		Assert.True( ClipFlags.IsTimed( "one-foot-early-out" ) );
	}

	[Fact]
	public void TheGraphTakesATimedFlagFromThePlayingClipBeforeItsFacts()
	{
		var branch = State( "Ollie", null, "ollie" );
		branch.Transitions.Add( new StateTransitionDefinition
		{
			Target = "Adjust",
			Guard = new StateTestDefinition { Op = "and", Items = { new StateTestDefinition { Test = "clip-flag", Flag = "can-branch", Mask = 1 } } }
		} );
		var definition = State( "Root", null, null, branch, State( "Adjust", null, "adjust" ) );
		var open = false;
		var graph = new StateGraph( definition ) { ClipFlag = flag => flag == "can-branch" ? open : null };
		var facts = new StateFacts();
		graph.Update( facts, 0.02f );
		Assert.Equal( "Ollie", graph.State );
		// The facts saying the branch is open do not count while the clip's window is shut.
		facts.Facts[ "clip-flag:can-branch" ] = 1f;
		graph.Update( facts, 0.02f );
		Assert.Equal( "Ollie", graph.State );
		open = true;
		graph.Update( facts, 0.02f );
		Assert.Equal( "Adjust", graph.State );
	}

	private static AnimationClipAttributeSet Set() => new( "clip", new uint[ 6 ], new[]
	{
		Attribute( "canbranch", 0.444f, 1f ),
		Attribute( "cantranstounderflip", 0.182f, 0.364f ),
		Attribute( "onefootearlyout", -1f, -1f )
	} );

	private static AnimationClipAttribute Attribute( string name, float start, float end ) =>
		new( AnimationAttributeKey.Encode( name ), start, end, 0, default );

	private static StateGraphDefinition State( string name, StateTestDefinition guard, string clip, params StateGraphDefinition[] children ) => new()
	{
		Name = name,
		Guard = guard,
		Play = clip is null ? null : new StatePlayDefinition { Clip = clip, BlendTime = 0f },
		States = children.ToList()
	};
}
