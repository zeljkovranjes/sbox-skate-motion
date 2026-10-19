using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BlendTreeTests
{
	[Fact]
	public void OneDimensionalBlendsMixTheTwoNeighbours()
	{
		var set = Set( ("slow", 31), ("medium", 31), ("fast", 61) );
		set.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "ride" ] = Blend( "blend", "speed", ("slow", null, null), ("medium", null, null), ("fast", null, null) )
		} );
		var ride = set.Find( "ride" ).Blend;
		var parameters = new AnimationParameters();
		parameters.Set( "speed", 0.75f );

		var weights = ride.Weights( parameters ).ToDictionary( w => w.Clip.Name, w => w.Weight );

		Assert.Equal( 2, weights.Count );
		Assert.Equal( 0.5f, weights[ "medium" ], 4 );
		Assert.Equal( 0.5f, weights[ "fast" ], 4 );
		Assert.Equal( 1.5f, ride.Duration( parameters ), 4 );
	}

	[Fact]
	public void NestedSelectAndSwitchPickOneChild()
	{
		var set = Set( ("plain", 31), ("styled", 31), ("left", 31), ("right", 31) );
		set.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "style" ] = Blend( "select", "rider", ("plain", null, "DEFAULT"), ("styled", null, "CASUAL") ),
			[ "turn" ] = Blend( "switch", "twist", ("left", -1f, null), ("style", 0f, null), ("right", 1f, null) )
		} );
		var turn = set.Find( "turn" ).Blend;
		var parameters = new AnimationParameters();

		parameters.Set( "twist", 0.2f );
		Assert.Equal( "plain", turn.Weights( parameters ).Single().Clip.Name );
		parameters.Set( "rider", "CASUAL" );
		Assert.Equal( "styled", turn.Weights( parameters ).Single().Clip.Name );
		parameters.Set( "twist", 0.9f );
		Assert.Equal( "right", turn.Weights( parameters ).Single().Clip.Name );
		Assert.Equal( 4, turn.Leaves.Count() );
	}

	[Fact]
	public void ChildrenListedInDescendingOrderBlendByTheirValues()
	{
		var set = Set( ("tall", 31), ("low", 31) );
		set.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "crouch" ] = Blend( "blend", "height", ("tall", 0.96f, null), ("low", 0.57f, null) )
		} );
		var crouch = set.Find( "crouch" ).Blend;
		var parameters = new AnimationParameters();

		parameters.Set( "height", 1.2f );
		Assert.Equal( "tall", crouch.Weights( parameters ).Single().Clip.Name );
		parameters.Set( "height", 0.57f );
		Assert.Equal( "low", crouch.Weights( parameters ).Single().Clip.Name );
		parameters.Set( "height", 0.765f );
		Assert.All( crouch.Weights( parameters ), w => Assert.Equal( 0.5f, w.Weight, 3 ) );
	}

	[Fact]
	public void UnknownChildrenAndCyclesAreRejected()
	{
		var set = Set( ("a", 31) );
		Assert.Throws<ArgumentException>( () => set.AddBlends( new Dictionary<string, BlendDefinition> { [ "x" ] = Blend( "blend", "p", ("missing", null, null) ) } ) );
		Assert.Throws<ArgumentException>( () => set.AddBlends( new Dictionary<string, BlendDefinition>
		{
			[ "x" ] = Blend( "blend", "p", ("y", null, null) ),
			[ "y" ] = Blend( "blend", "p", ("x", null, null) )
		} ) );
	}

	private static BlendDefinition Blend( string type, string parameter, params (string Name, float? At, string Key)[] children ) => new()
	{
		Type = type,
		Parameter = parameter,
		Children = children.Select( c => new BlendChildDefinition { Name = c.Name, At = c.At, Key = c.Key } ).ToList()
	};

	// Clips that hold the rest pose, with the given frame counts at 30 frames per second.
	private static ClipSet Set( params (string Name, int Frames)[] clips )
	{
		var skeleton = new ClipSkeleton( new[] { "root" }, new[] { -1 }, new[] { Vector3.Zero }, new[] { Quaternion.Identity } );
		return new ClipSet( skeleton, clips.Select( c => new AnimationClip( c.Name, 30f, c.Frames, new[] { 0 }, new[] { 0 },
			Array.Empty<Quaternion>(), new[] { 0 }, new[] { 0 }, Array.Empty<Vector3>() ) ) );
	}
}
