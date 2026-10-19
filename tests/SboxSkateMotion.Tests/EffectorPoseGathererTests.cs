using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class EffectorPoseGathererTests
{
	[Fact]
	public void UntouchedAnimatedBonesFollowTheSuppliedMapping()
	{
		var clips = new[] { At( 1f ), At( 2f ), At( 3f ) };
		var animated = new LimbRigidTransform[ 2 ];
		var mutable = new LimbRigidTransform[ 2 ];
		EffectorPoseGatherer.GatherBones( new[] { 2, 0 }, new[] { At( 5f ), At( 9f ) }, clips, animated, mutable );
		Assert.Equal( new[] { At( 3f ), At( 1f ) }, animated );
		Assert.Equal( new[] { At( 8f ), At( 10f ) }, mutable );
		Assert.Equal( new[] { At( 1f ), At( 2f ), At( 3f ) }, clips );
	}

	[Fact]
	public void BindTranslationIsRotatedByTheAnimatedPose()
	{
		var turn = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 4f, 5f, 6f ) );
		var animated = new LimbRigidTransform[ 1 ];
		var mutable = new LimbRigidTransform[ 1 ];
		EffectorPoseGatherer.GatherBones( new[] { 0 }, new[] { At( 2f ) }, new[] { turn }, animated, mutable );
		Assert.Equal( new Vector3( 4f, 7f, 6f ), mutable[ 0 ].Translation );
		Assert.Equal( turn, animated[ 0 ] );
	}

	[Fact]
	public void GatheringDoesNotNormalizeAffineAxes()
	{
		var frame = new LimbFrame( 2f * Vector3.UnitX, new Vector3( 1f, 3f, 0f ), 4f * Vector3.UnitZ );
		var pose = new LimbRigidTransform( frame, Vector3.Zero );
		var animated = new LimbRigidTransform[ 1 ];
		var mutable = new LimbRigidTransform[ 1 ];
		EffectorPoseGatherer.GatherBones( new[] { 0 }, new[] { LimbRigidTransform.Identity }, new[] { pose }, animated, mutable );
		Assert.Equal( frame, mutable[ 0 ].Rotation );
	}

	[Fact]
	public void NewGatherOverwritesPreviousSolvedPose()
	{
		var animated = new LimbRigidTransform[ 1 ];
		var mutable = new[] { At( 100f ) };
		EffectorPoseGatherer.GatherBones( new[] { 0 }, new[] { At( 2f ) }, new[] { At( 3f ) }, animated, mutable );
		mutable[ 0 ] = At( 200f );
		EffectorPoseGatherer.GatherBones( new[] { 0 }, new[] { At( 2f ) }, new[] { At( 4f ) }, animated, mutable );
		Assert.Equal( At( 4f ), animated[ 0 ] );
		Assert.Equal( At( 6f ), mutable[ 0 ] );
	}

	[Fact]
	public void AuthoredTargetsUseHelperClipIndicesAndIndependentEndpointBindings()
	{
		var clips = Enumerable.Range( 0, 8 ).Select( i => At( i ) ).ToArray();
		var authored = new LimbRigidTransform[ 4 ];
		EffectorPoseGatherer.GatherAuthored( new[] { 7, 5, 6, 4 }, new[] { 1, 0, 1, 0 },
			new[] { At( 10f ), At( 20f ) }, clips, authored );
		Assert.Equal( new[] { At( 27f ), At( 15f ), At( 26f ), At( 14f ) }, authored );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 2 )]
	public void InvalidGatherMappingFailsBeforeWritingAnyOutput( int invalid )
	{
		var animated = new[] { At( 30f ), At( 40f ) };
		var mutable = new[] { At( 50f ), At( 60f ) };
		Assert.Throws<ArgumentException>( () => EffectorPoseGatherer.GatherBones( new[] { 0, invalid },
			new[] { At( 1f ), At( 2f ) }, new[] { At( 3f ), At( 4f ) }, animated, mutable ) );
		Assert.Equal( new[] { At( 30f ), At( 40f ) }, animated );
		Assert.Equal( new[] { At( 50f ), At( 60f ) }, mutable );
	}

	[Fact]
	public void AnimatedAndMutableOutputsCannotAlias()
	{
		var output = new LimbRigidTransform[ 1 ];
		Assert.Throws<ArgumentException>( () => EffectorPoseGatherer.GatherBones( new[] { 0 },
			new[] { At( 1f ) }, new[] { At( 2f ) }, output, output ) );
	}

	[Fact]
	public void GatheringCannotOverwriteTheClipSource()
	{
		var source = new[] { At( 1f ) };
		Assert.Throws<ArgumentException>( () => EffectorPoseGatherer.GatherBones( new[] { 0 },
			new[] { At( 2f ) }, source, new LimbRigidTransform[ 1 ], source ) );
	}

	[Fact]
	public void InvalidAuthoredBindingFailsBeforeWritingAnyOutput()
	{
		var output = Enumerable.Repeat( At( 50f ), 4 ).ToArray();
		Assert.Throws<ArgumentException>( () => EffectorPoseGatherer.GatherAuthored( new[] { 0, 0, 0, 0 },
			new[] { 0, 0, 0, 1 }, new[] { At( 1f ) }, new[] { At( 2f ) }, output ) );
		Assert.All( output, pose => Assert.Equal( At( 50f ), pose ) );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, 0f, 0f ) };
}
