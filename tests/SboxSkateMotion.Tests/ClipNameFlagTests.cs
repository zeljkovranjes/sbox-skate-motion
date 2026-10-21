using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ClipNameFlagTests
{
	[Fact]
	public void AFlagNamedAfterThePlayingClipIsTrue()
	{
		// The graph lands a jump with the landing made for that jump by testing the jump clip's own name.
		var jump = Clip( "JBR_STAND_0_TO_BIG_FWD_0" );
		var parameters = new AnimationParameters();
		Assert.True( ClipFlags.Active( jump, parameters, 0.2f, "jbr-stand-0-to-big-fwd-0" ) );
		// Another clip's name is not known from this clip: the facts decide it.
		Assert.Null( ClipFlags.Active( jump, parameters, 0.2f, "jbr-stand-0-to-sml-dn-0" ) );
		Assert.Null( ClipFlags.Active( null, parameters, 0.2f, "jbr-stand-0-to-big-fwd-0" ) );
	}

	[Fact]
	public void ALandingStateFollowsTheJumpThatPlayed()
	{
		var land = new StateGraphDefinition
		{
			Name = "Landing",
			States =
			{
				Landing( "BigLanding", "jump-big" ),
				Landing( "SmallLanding", "jump-small" )
			}
		};
		var definition = new StateGraphDefinition { Name = "Root", States = { land } };
		var playing = Clip( "JUMP_SMALL" );
		var parameters = new AnimationParameters();
		// Entered on the first update, once the graph can read the playing clip.
		var graph = new StateGraph( definition, deferInitialEntry: true ) { ClipFlag = flag => ClipFlags.Active( playing, parameters, 0f, flag ) };
		graph.Update( new StateFacts(), 0.02f );
		Assert.Equal( "Landing.SmallLanding", graph.State );
	}

	[Fact]
	public void ABlendChosenInsideTheClipNamesItsOwnLanding()
	{
		// A jump out picks its way by a switch, each way a blend of a high and a low take; the landings name the ways.
		var skeleton = new ClipSkeleton( new[] { "root" }, new[] { -1 }, new[] { Vector3.Zero }, new[] { Quaternion.Identity } );
		var set = new ClipSet( skeleton, new[] { "FWD_HI", "FWD_LO", "BWD_HI", "BWD_LO" }.Select( name => new AnimationClip( name, 30f, 31,
			new[] { 0 }, new[] { 0 }, Array.Empty<Quaternion>(), new[] { 0 }, new[] { 0 }, Array.Empty<Vector3>() ) ) );
		set.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "JUMPOUT_FWD" ] = Blend( "blend", "crouch", ("FWD_HI", 0f), ("FWD_LO", 1f) ),
			[ "JUMPOUT_BWD" ] = Blend( "blend", "crouch", ("BWD_HI", 0f), ("BWD_LO", 1f) ),
			[ "JUMPOUT" ] = Blend( "switch", "angle", ("JUMPOUT_FWD", 0f), ("JUMPOUT_BWD", 180f) )
		} );
		var jumpout = set.Find( "JUMPOUT" );
		var parameters = new AnimationParameters();
		parameters.Set( "angle", 170f );
		Assert.True( ClipFlags.Active( jumpout, parameters, 0.1f, "jumpout-bwd" ) );
		Assert.True( ClipFlags.Active( jumpout, parameters, 0.1f, "bwd-hi" ) );
		Assert.Null( ClipFlags.Active( jumpout, parameters, 0.1f, "jumpout-fwd" ) );
		parameters.Set( "angle", 10f );
		parameters.Set( "crouch", 1f );
		Assert.True( ClipFlags.Active( jumpout, parameters, 0.1f, "jumpout-fwd" ) );
		// The blend's other end carries no weight.
		Assert.Null( ClipFlags.Active( jumpout, parameters, 0.1f, "fwd-hi" ) );
		Assert.True( ClipFlags.Active( jumpout, parameters, 0.1f, "fwd-lo" ) );
	}

	private static BlendDefinition Blend( string type, string parameter, params (string Name, float At)[] children ) => new()
	{
		Type = type,
		Parameter = parameter,
		Children = children.Select( c => new BlendChildDefinition { Name = c.Name, At = c.At } ).ToList()
	};

	private static StateGraphDefinition Landing( string name, string jump ) => new()
	{
		Name = name,
		Guard = new StateTestDefinition { Op = "and", Items = { new StateTestDefinition { Test = "clip-flag", Flag = jump, Mask = 1 } } },
		Play = new StatePlayDefinition { Clip = name, BlendTime = 0f }
	};

	private static AnimationClip Clip( string name ) => new( name, 30f, 1, Array.Empty<int>(), Array.Empty<int>(),
		Array.Empty<Quaternion>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<Vector3>() );
}
