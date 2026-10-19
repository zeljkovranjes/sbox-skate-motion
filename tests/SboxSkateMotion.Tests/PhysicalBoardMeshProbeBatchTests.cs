using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardMeshProbeBatchTests
{
	[Fact]
	public void MeshBatchPublishesWheelAndGroundHitsThenRetainsCachesAcrossAFilteredMiss()
	{
		var batch = new PhysicalBoardMeshProbeBatch( 5 );
		var probes = new PhysicalBoardProbeState();
		var contacts = new PhysicalBoardProbeContactState();
		var request = new PhysicalBoardProbe( new( 0f, .1f, 0f ), new( 0f, -.1f, 0f ), 0f, 7, 3, 0, 0 );
		batch.Begin();
		for ( var index = 0; index < 5; index++ ) Assert.True( batch.TryAdd( request ) );
		Assert.False( batch.TryAdd( request ) );
		batch.Execute( Query(), [ Floor( 7, 0 ) ] );
		Assert.Equal( 2, batch.State );
		PhysicalBoardProbeResultConsumer.Update( probes, contacts, batch );
		Assert.Equal( 0, batch.State );
		Assert.Equal( 5, batch.Count );
		Assert.True( probes.Ground.Valid );
		Assert.Equal( 0x183, probes.Ground.Material );
		Assert.Equal( Vector3.UnitY, probes.Ground.Normal );
		Assert.All( contacts.Distances, distance => Assert.Equal( .1f, distance ) );
		var position = probes.Ground.Position;
		batch.Begin();
		Assert.Equal( 1, batch.State );
		Assert.Equal( 0, batch.Count );
		for ( var index = 0; index < 5; index++ ) batch.TryAdd( request with { ExcludedActor = 8 } );
		batch.Execute( Query(), [ Floor( 7, 0 ) ] );
		PhysicalBoardProbeResultConsumer.Update( probes, contacts, batch );
		Assert.False( probes.Ground.Valid );
		Assert.Equal( position, probes.Ground.Position );
		Assert.Equal( 0x183, probes.Ground.Material );
		Assert.Equal( .2f, contacts.ClosestDistance );
	}

	[Fact]
	public void FacingBitsAndInstanceExclusionsRemainDistinctFromTheActorFilter()
	{
		var batch = new PhysicalBoardMeshProbeBatch( 3 );
		var request = new PhysicalBoardProbe( new( 0f, .1f, 0f ), new( 0f, -.1f, 0f ), 0f, 7, 3, 0, 0 );
		batch.Begin();
		Assert.Throws<ArgumentException>( () => batch.TryAdd( request with { Auxiliary = 1 } ) );
		Assert.Equal( 0, batch.Count );
		batch.TryAdd( request );
		batch.TryAdd( request with { Flags = 4 } );
		batch.TryAdd( request with { Filter = 0 } );
		batch.Execute( Query(), [ Floor( 7, 4 ) ] );
		batch.Complete();
		Assert.True( batch.TryGet( 0, out var hit ) );
		Assert.Equal( .5f, hit.Fraction );
		Assert.False( batch.TryGet( 1, out _ ) );
		Assert.False( batch.TryGet( 2, out _ ) );
	}

	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );

	private static MeshQueryInstance Floor( int filter, uint exclusion )
	{
		var first = new Vector3( -3f, 0f, -3f );
		var second = new Vector3( -3f, 0f, 3f );
		var third = new Vector3( 3f, 0f, 3f );
		var fourth = new Vector3( 3f, 0f, -3f );
		var geometry = new MeshQueryGeometry( [ new( first, second, third, 0x183 ), new( first, third, fourth, 0x183 ) ], first, third );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0f;
		return new( geometry, identity, identity, first, third, filter, exclusion, 123 );
	}
}
