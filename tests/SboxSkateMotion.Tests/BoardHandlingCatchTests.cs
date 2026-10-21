using System;
using System.IO;
using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingCatchTests
{
	[Theory]
	[InlineData( 0 )]
	[InlineData( 3 )]
	[InlineData( 8 )]
	[InlineData( 30 )]
	public void CatchingTheBoardWhileTheReachHandsOverNeverJumpsThePose( int framesIntoTheCycle )
	{
		// The reach moves the bone from 0 to 1 over a second, the cycle holds it at 1 and the out returns it to 0.
		var layer = new BoardHandlingAnimationLayer( Bank(), Graph() );
		var pose = new SkeletonPose( 1 );
		var previous = 0f;
		var largest = 0f;
		var cycleAt = -1;
		var sawOut = false;
		for ( var frame = 0; frame < 150; frame++ )
		{
			pose.Positions[ 0 ] = Vector3.Zero;
			pose.Rotations[ 0 ] = Quaternion.Identity;
			var holding = cycleAt >= 0 && frame > cycleAt + framesIntoTheCycle;
			layer.Update( new BoardHandlingInput( false, holding, false, true, false, false, frame == 0, 0f, 0f, false ), 1f / 60f, pose );
			if ( cycleAt < 0 && layer.Phase == BoardHandlingPhase.RetrieveCycle )
				cycleAt = frame;
			sawOut |= layer.Motion == BoardHandlingMotion.FrontOut;
			if ( frame > 0 )
				largest = MathF.Max( largest, MathF.Abs( pose.Positions[ 0 ].X - previous ) );
			previous = pose.Positions[ 0 ].X;
		}
		Assert.True( cycleAt > 0 );
		Assert.True( sawOut );
		// The bone never moves more in a frame than the fades and clips move it (a cut-short reach jumped 0.2).
		Assert.True( largest < .12f, $"largest step {largest}" );
	}

	private static AnimationGraphDefinition Graph()
	{
		var result = new AnimationGraphDefinition { BoardHandling = new() };
		foreach ( var side in new[] { "Front", "Back" } )
		{
			result.BoardHandling.Motions.Add( side + "Into", "into" );
			result.BoardHandling.Motions.Add( side + "Cycle", "cycle" );
			result.BoardHandling.Motions.Add( side + "Out", "out" );
		}
		return result;
	}

	// Three one-bone clips at two frames a second whose bone runs along X: a reach 0 to 1, a held 1, an out 1 to 0.
	private static RawAnimationClipSet Bank()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( 0x4e414d53u );
		writer.Write( 2u );
		writer.Write( (ushort)1 );
		Text( writer, "root" );
		writer.Write( (short)-1 );
		writer.Write( (ushort)1 );
		foreach ( var value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
			writer.Write( value );
		var clips = new (string Name, float[] Along)[] { ("into", new[] { 0f, .5f, 1f }), ("cycle", new[] { 1f, 1f, 1f }), ("out", new[] { 1f, .5f, 0f }) };
		writer.Write( (uint)clips.Length );
		foreach ( var (name, along) in clips )
		{
			Text( writer, name );
			writer.Write( 2f );
			writer.Write( 1f );
			writer.Write( (ushort)along.Length );
			writer.Write( (ushort)0 );
			writer.Write( (byte)1 );
			writer.Write( (byte)0 );
			writer.Write( new byte[] { 2, 0, 0 } );
			foreach ( var x in along )
			{
				foreach ( var value in new[] { x, 0f, 0f, 1f } )
					writer.Write( value );
			}
		}
		return RawAnimationClipSet.Read( stream.ToArray() );
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
