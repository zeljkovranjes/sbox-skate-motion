using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQuerySamplesTests
{
	[Fact]
	public void InactiveOwnerResetsDefinedOutputsWithoutTouchingPaddingOrQueryServices()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 0x7c / 4 ] = 0x12345678;
		owner[ 0x70 / 4 ] = 0x3f800000;
		owner[ 0x6294 / 4 ] = 3;
		var calls = 0;
		var results = new PhysicalGroundQueryResults( () => calls++, () => calls++,
			_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
			(_, _, _, _) => throw new InvalidOperationException() );
		Assert.Equal( -1, PhysicalGroundQuerySamples.Update( owner, results, Angles() ) );
		Assert.Equal( 0, calls );
		Assert.Equal( 0u, owner[ 0x70 / 4 ] );
		Assert.Equal( 0x12345678u, owner[ 0x7c / 4 ] );
		Assert.Equal( 30u, owner[ 0x140 / 4 ] );
		Assert.Equal( 3u, owner[ 0x6294 / 4 ] );
	}

	[Fact]
	public void CompletedQueriesPreserveBatchOrderingAndPublishSupportIdentity()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 3 ] = owner[ 4 ] = owner[ 9 ] = owner[ 14 ] = 0x3f800000;
		owner[ 0x13c / 4 ] = 0x01010203;
		owner[ 0x2994 / 4 ] = 7;
		owner[ 0x29e4 / 4 ] = 8;
		owner[ 0x2a34 / 4 ] = 9;
		var hit = new uint[ 36 ];
		hit[ 3 ] = hit[ 9 ] = 0x3f800000;
		hit[ 16 ] = hit[ 21 ] = hit[ 26 ] = hit[ 31 ] = 0x3f800000;
		hit[ 34 ] = 123;
		var calls = new List<string>();
		var results = new PhysicalGroundQueryResults( () => calls.Add( "support complete" ),
			() => calls.Add( "sample complete" ), id =>
			{
				calls.Add( $"support {id}" );
				return id == 7 ? hit : ReadOnlyMemory<uint>.Empty;
			}, _ => throw new InvalidOperationException(), (_, _, _, _) =>
			{
				calls.Add( "edges" );
				return ReadOnlyMemory<uint>.Empty;
			} );
		Assert.Equal( 0, PhysicalGroundQuerySamples.Update( owner, results, Angles() ) );
		Assert.Equal( new[] { "support complete", "sample complete", "support 7", "support 8", "support 9", "edges" }, calls );
		Assert.Equal( 0x00010203u, owner[ 0x13c / 4 ] );
		Assert.Equal( 123u, owner[ 0x124 / 4 ] );
		Assert.Equal( 1u, owner[ 0x76f0 / 4 ] );
		Assert.Equal( 1u, owner[ 0x120 / 4 ] & 1 );
	}

	[Fact]
	public void ProjectedSamplesRetainPaddingAndDistinguishOverheadFromAuxiliary()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		owner[ 4 ] = owner[ 9 ] = owner[ 14 ] = 0x3f800000;
		owner[ 0x2a88 / 4 ] = 0x11223344;
		var normal = Vector4.UnitY;
		PhysicalGroundQuerySamples.Append( owner, new Vector4( 1f, 0.9f, 0f, 1f ), ref normal, 0 );
		PhysicalGroundQuerySamples.Append( owner, new Vector4( 0.5f, 0.1f, 0f, 1f ), ref normal, 2 );
		Assert.Equal( 1u, owner[ 0x3a50 / 4 ] );
		Assert.Equal( 1u, owner[ 0x4a60 / 4 ] );
		Assert.Equal( 8u, owner[ 0x2a80 / 4 ] );
		Assert.Equal( 16u, owner[ 0x3a90 / 4 ] );
		Assert.Equal( 0x3f800000u, owner[ 0x2a7c / 4 ] );
		Assert.Equal( 0x11223344u, owner[ 0x2a88 / 4 ] );
	}

	private static LimbSolverMath Angles() => new( new float[ 3 ], new float[ 3 ], new float[ 8 ], 1f, 1f, 0f, MathF.PI );
}
