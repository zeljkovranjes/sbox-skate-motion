using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalGroundQueryRequestsTests
{
	[Fact]
	public void DescriptorIdsRetainRejectedTailAndCallerPadding()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		Array.Fill( owner, 0x3e123456u );
		PhysicalGroundQueryRequests.Initialize( owner );
		Assert.Equal( 15u, owner[ 0x2a40 / 4 ] );
		Assert.Equal( 23u, owner[ 0x2a44 / 4 ] );
		Assert.Equal( 43u, owner[ (0x150 + 14 * 80 + 68) / 4 ] );
		Assert.Equal( 24u, owner[ (0x150 + 72) / 4 ] );
		Assert.Equal( 0x3e123456u, owner[ (0x150 + 12) / 4 ] );
		Assert.Equal( 0x00123456u, owner[ (0x150 + 76) / 4 ] );
	}

	[Fact]
	public void MeshResultsRefreshWithoutRenumberingOverflowRequests()
	{
		var owner = new uint[ PhysicalGroundQuerySelection.OwnerWordCount ];
		PhysicalGroundQueryRequests.Initialize( owner );
		owner[ 0x6298 / 4 ] = uint.MaxValue;
		var frame = new float[] { -1, .05f, -1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0,
			0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 };
		var words = Array.ConvertAll( frame, BitConverter.SingleToUInt32Bits );
		var mesh = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		var trajectory = new TrajectoryWorldQuery( mesh, new( 1f / 60f, .0001f, .7f, 1e-6f ) );
		var batch = new PhysicalGroundMeshQueryBatch( (_, _, _, _) => ReadOnlyMemory<uint>.Empty );
		var matrix = Matrix4x4.Identity;
		matrix.M44 = 0;
		var geometry = new MeshQueryGeometry( [new( new( -5, 0, -5 ), new( -5, 0, 5 ), new( 5, 0, -5 ), 3 )], new( -5 ), new( 5 ) );
		var instance = new MeshQueryInstance( geometry, matrix, matrix, new( -5 ), new( 5 ), -1, 0, 123 );
		batch.Issue( owner, words, mesh, trajectory, [instance] );
		Assert.Equal( 38, batch.SampleCount );
		Assert.Equal( 2, batch.SupportState );
		Assert.False( batch.Results.GetSupport( 0 ).IsEmpty );
		Assert.False( batch.Results.GetSample( 0 ).IsEmpty );
		Assert.True( batch.Results.GetSample( 38 ).IsEmpty );
		Assert.True( batch.Results.GetSample( 43 ).IsEmpty );
		batch.Results.CompleteSupportQueries();
		Assert.Equal( 0, batch.SupportState );
		batch.Issue( owner, words, mesh, trajectory, [instance with { Exclusion = 0x2000 }] );
		Assert.True( batch.Results.GetSupport( 0 ).IsEmpty );
		Assert.True( batch.Results.GetSample( 0 ).IsEmpty );
		Assert.Equal( 0x01000000u, owner[ 0x13c / 4 ] );
	}

	[Fact]
	public void DownwardAccelerationStillRejectsZeroStepControls()
	{
		var mesh = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		var trajectory = new TrajectoryWorldQuery( mesh, new( 1f / 60f, .0001f, .7f, 1e-6f ) );
		var request = new TrajectoryQueryRequest( Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY,
			1f, .01f, 0f, 0f, -1, 0, new( 0, 0, 0, 0, true, true ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => trajectory.Evaluate( request, [] ) );
	}
}
