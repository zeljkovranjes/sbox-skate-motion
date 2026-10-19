using System;
using System.IO;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingAnimationLayerTests
{
	[Fact]
	public void PickupLayersReachCycleAndReturnOverLocomotion()
	{
		var layer = Layer();
		var pose = BasePose();
		var sawCycle = false;
		var sawOut = false;
		var maximum = 0f;
		for ( var frame = 0; frame < 180; frame++ )
		{
			pose.Positions[ 0 ] = Vector3.Zero;
			layer.Update( Input( frame == 0, frame >= 65 ), 1f / 60f, pose );
			sawCycle |= layer.Phase == BoardHandlingPhase.RetrieveCycle;
			sawOut |= layer.Phase == BoardHandlingPhase.RetrieveOut;
			maximum = MathF.Max( maximum, pose.Positions[ 0 ].X );
		}
		Assert.True( sawCycle );
		Assert.True( sawOut );
		Assert.InRange( maximum, .99f, 1.01f );
		Assert.False( layer.Active );
		Assert.Equal( BoardHandlingPhase.Idle, layer.Phase );
	}

	[Fact]
	public void CancellationFadesTheOverlayAndLeavesBasePoseAvailable()
	{
		var layer = Layer();
		var pose = BasePose();
		for ( var frame = 0; frame < 60; frame++ )
			layer.Update( Input( frame == 0, false ), 1f / 60f, pose );
		Assert.Equal( BoardHandlingPhase.RetrieveCycle, layer.Phase );
		for ( var frame = 0; frame < 30; frame++ )
			layer.Update( Input( false, false ) with { RetrievalActive = false }, 1f / 60f, pose );
		Assert.False( layer.Active );
		pose.Positions[ 0 ] = new Vector3( 9f, 8f, 7f );
		layer.Update( Input( false, false ) with { RetrievalActive = false }, 1f / 60f, pose );
		Assert.Equal( new Vector3( 9f, 8f, 7f ), pose.Positions[ 0 ] );
	}

	[Fact]
	public void RepeatedPickupAndPendingCycleReplacementReuseStorage()
	{
		var layer = Layer();
		var pose = BasePose();
		for ( var repetition = 0; repetition < 12; repetition++ )
		{
			for ( var frame = 0; frame < 420; frame++ )
				layer.Update( Input( frame == 0, frame >= 300 ), 1f / 60f, pose );
			Assert.False( layer.Active );
			Assert.Equal( BoardHandlingPhase.Idle, layer.Phase );
		}
	}

	[Fact]
	public void MissingDropBindingDoesNotInventAMotion()
	{
		var layer = Layer();
		layer.Update( Input( false, true ) with { DropRequested = true }, .1f, BasePose() );
		Assert.False( layer.Active );
		Assert.Equal( BoardHandlingPhase.Idle, layer.Phase );
	}

	[Fact]
	public void MissingRetrievalPhaseIsRejectedAtLoad()
	{
		var graph = Graph();
		graph.BoardHandling.Motions.Remove( "BackOut" );
		Assert.Throws<ArgumentException>( () => new BoardHandlingAnimationLayer( Bank(), graph ) );
	}

	private static BoardHandlingInput Input( bool request, bool holding ) =>
		new( false, holding, false, true, false, false, request, 0f, 0f, false );

	private static BoardHandlingAnimationLayer Layer() => new( Bank(), Graph() );

	private static AnimationGraphDefinition Graph()
	{
		var result = new AnimationGraphDefinition { BoardHandling = new() };
		foreach ( var name in new[] { "FrontInto", "FrontCycle", "FrontOut", "BackInto", "BackCycle", "BackOut" } )
			result.BoardHandling.Motions.Add( name, "reach" );
		return result;
	}

	private static SkeletonPose BasePose()
	{
		var pose = new SkeletonPose( 1 );
		pose.Rotations[ 0 ] = Quaternion.Identity;
		return pose;
	}

	private static RawAnimationClipSet Bank()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( 0x4e414d53u );
		writer.Write( 2u );
		writer.Write( (ushort)1 );
		writer.Write( (byte)4 );
		writer.Write( "root"u8.ToArray() );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		foreach ( var value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
		writer.Write( 1u );
		writer.Write( (byte)5 );
		writer.Write( "reach"u8.ToArray() );
		writer.Write( 2f );
		writer.Write( 1f );
		writer.Write( (ushort)3 );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		writer.Write( new byte[] { 1, 1, 1 } );
		foreach ( var value in new[] { 2f, 0f, 0f, .5f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
		return RawAnimationClipSet.Read( stream.ToArray() );
	}
}
