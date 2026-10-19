using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQueryContextTests
{
	[Fact]
	public void FrameUsesSecondaryContactPositionAndIndependentQueryAxes()
	{
		var owner = new PhysicalLocomotionOwnerState
		{
			Frame = Matrix4x4.Identity,
			SecondaryFrame = Matrix4x4.CreateTranslation( 2, 1, 3 ),
			Solved = new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new( 8, 9, 10 ), new( 3, 4, 5 ) )
		};
		var adopted = new PhysicalLocomotionFrameState { QueryAxes = new( -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX ) };
		var retained = new uint[ PhysicalGroundQueryPublication.RecordWordCount ];
		retained[ 0 ] = BitConverter.SingleToUInt32Bits( 7 );
		retained[ 1 ] = BitConverter.SingleToUInt32Bits( 2 );
		retained[ 2 ] = BitConverter.SingleToUInt32Bits( 8 );
		retained[ 0xb0 / 4 ] = 1;
		var context = new PhysicalGroundQueryContext( 9, true );
		context.Capture( owner, Vector3.UnitZ, adopted, retained );
		Assert.Equal( new Vector3( 2, 2, 3 ), Read( context.Frame, 0 ) );
		Assert.Equal( Vector3.UnitZ, Read( context.Frame, 4 ) );
		Assert.Equal( -Vector3.UnitZ, Read( context.Frame, 24 ) );
		Assert.Equal( new Vector3( 3, 4, 5 ), Read( context.Frame, 16 ) );
		var queryOwner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		context.BindOwner( queryOwner );
		Assert.Equal( 1u, queryOwner[ 0x138 / 4 ] );
		Assert.Equal( 9u, queryOwner[ 0x6298 / 4 ] );
		Assert.Equal( new Vector3( 2, 1, 3 ), owner.SecondaryFrame.Translation );
	}

	[Fact]
	public void RegisteredProvidersShareOrderedOutputAndRetainedFlags()
	{
		var calls = 0;
		void First( GrindEdgeBounds bounds, uint world, int filter, Span<uint> output, ref int count )
		{
			Assert.Equal( 0, calls++ );
			Assert.Equal( 1u, world );
			Assert.Equal( 9, filter );
			Assert.Equal( 0, count );
			output[ 8 ] = 31;
			output[ 9 ] = 0x12345678;
			count++;
		}
		void Second( GrindEdgeBounds bounds, uint world, int filter, Span<uint> output, ref int count )
		{
			Assert.Equal( 1, calls++ );
			Assert.Equal( 1, count );
			Assert.Equal( 31u, output[ 8 ] );
			output[ 20 ] = 32;
			count++;
		}
		var registry = new PhysicalGroundRegisteredEdges( [], First, Second );
		var result = registry.Collect( new( -1 ), new( 1 ), 1, 9 );
		Assert.Equal( 24, result.Length );
		Assert.Equal( 0x12345678u, result.Span[ 9 ] );
		Assert.Equal( 32u, result.Span[ 20 ] );
		Assert.Equal( 2, calls );
	}

	private static Vector3 Read( ReadOnlySpan<uint> words, int offset ) => new(
		BitConverter.UInt32BitsToSingle( words[ offset ] ), BitConverter.UInt32BitsToSingle( words[ offset + 1 ] ),
		BitConverter.UInt32BitsToSingle( words[ offset + 2 ] ) );
}
