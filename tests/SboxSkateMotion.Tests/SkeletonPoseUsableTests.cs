using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SkeletonPoseUsableTests
{
	[Fact]
	public void APoseNotYetWrittenIsNotUsable()
	{
		Assert.False( new SkeletonPose( 3 ).Usable );
	}

	[Fact]
	public void ARestPoseIsUsable()
	{
		var skeleton = new ClipSkeleton( new[] { "root", "hips", "spine" }, new[] { -1, 0, 1 },
			new[] { Vector3.Zero, Vector3.UnitZ, Vector3.UnitZ }, new[] { Quaternion.Identity, Quaternion.Identity, Quaternion.Identity } );
		var pose = new SkeletonPose( 3 );
		pose.SetRest( skeleton );
		Assert.True( pose.Usable );
	}

	[Fact]
	public void ABoneBlendedFromNothingMakesThePoseUnusable()
	{
		var pose = new SkeletonPose( 2 );
		pose.Rotations[ 0 ] = Quaternion.Identity;
		pose.Rotations[ 1 ] = new Quaternion( float.NaN, 0f, 0f, 1f );
		Assert.False( pose.Usable );
	}

	[Fact]
	public void ABonePlacedAtInfinityMakesThePoseUnusable()
	{
		var pose = new SkeletonPose( 1 );
		pose.Rotations[ 0 ] = Quaternion.Identity;
		pose.Positions[ 0 ] = new Vector3( 0f, float.PositiveInfinity, 0f );
		Assert.False( pose.Usable );
	}
}
