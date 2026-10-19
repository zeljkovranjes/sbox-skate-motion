using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardEffectorTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void AuthoredTargetsFollowTheBoardFrameAndSwapWhenMirrored( bool mirror )
	{
		var names = new[] { "root", "hips", "left_toe", "right_toe", "board", "target_left_foot", "target_right_foot" };
		var parents = new[] { -1, 0, 1, 1, 0, 4, 4 };
		var rest = new Vector3[] { Vector3.Zero, Vector3.UnitZ, new( 0f, 0.2f, -0.9f ),
			new( 0f, -0.2f, -0.9f ), new( 0.3f, 0f, 0.1f ), new( 0.2f, 0.1f, 0.04f ), new( -0.1f, -0.2f, 0.06f ) };
		var rotations = Enumerable.Repeat( Quaternion.Identity, names.Length ).ToArray();
		rotations[ 4 ] = Quaternion.CreateFromYawPitchRoll( 0.7f, -0.4f, 0.3f );
		var skeleton = new ClipSkeleton( names, parents, rest, rotations );
		var targetNames = new[] { "hips", "shoe_left", "shoe_right" };
		var map = new Dictionary<string, string>
		{
			[ "hips" ] = "hips", [ "shoe_left" ] = "left_toe", [ "shoe_right" ] = "right_toe"
		};
		var retargeter = new PoseRetargeter( skeleton, targetNames, new[] { -1, 0, 0 },
			new[] { Vector3.UnitZ, new Vector3( 0f, 0.2f, 0.1f ), new Vector3( 0f, -0.2f, 0.1f ) },
			Enumerable.Repeat( Quaternion.Identity, 3 ).ToArray(), map );
		var pose = new SkeletonPose( names.Length );
		pose.SetRest( skeleton );
		retargeter.Apply( pose, mirror, new Vector3[ 3 ], new Quaternion[ 3 ] );
		var source = retargeter.SourceEffector( 1, mirror );
		Assert.Equal( mirror ? 6 : 5, source );
		var expected = rest[ source ];
		if ( mirror ) expected.Y = -expected.Y;
		Assert.True( Vector3.Distance( expected, retargeter.SourceRelative( source, 4, mirror ) ) < 1e-5f );
		Assert.NotEqual( retargeter.SourceBone( 1, mirror ), source );
	}

	[Fact]
	public void AClipWithoutHelpersKeepsItsMappedEffector()
	{
		var names = new[] { "hips", "left_toe" };
		var parents = new[] { -1, 0 };
		var rotations = new[] { Quaternion.Identity, Quaternion.Identity };
		var skeleton = new ClipSkeleton( names, parents, new[] { Vector3.UnitZ, new Vector3( 0f, 0f, -0.9f ) }, rotations );
		var retargeter = new PoseRetargeter( skeleton, names, parents,
			new[] { Vector3.UnitZ, new Vector3( 0f, 0f, 0.1f ) }, rotations,
			names.ToDictionary( name => name, name => name ) );
		Assert.Equal( 1, retargeter.SourceEffector( 1, false ) );
		Assert.Equal( 1, retargeter.SourceEffector( 1, true ) );
	}
}
