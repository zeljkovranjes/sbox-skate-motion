using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HandPoseLayerTests
{
	private static readonly AnimationPoseStreamKey Left = AnimationPoseStreamKey.Encode( "LEFTHAND" );
	private static readonly AnimationPoseStreamKey Right = AnimationPoseStreamKey.Encode( "RIGHTHAND" );

	// Pose index per bank frame: the finger joint bends about X by this many degrees.
	private static readonly float[] Bends = { 0f, 20f, 40f, 60f, 80f };

	[Fact]
	public void StreamNamesEncodeAsAuthoredTracksStoreThem()
	{
		Assert.Equal( new AnimationPoseStreamKey( 1775372703, 1930835936, 0, 0, 0, 0 ), Left );
		Assert.Equal( new AnimationPoseStreamKey( 2259162262, 922398320, 0, 0, 0, 0 ), Right );
	}

	[Fact]
	public void BankBonesJoinTheSkeletonBelowTheirHands()
	{
		var layer = Layer();
		var skeleton = layer.Skeleton;
		Assert.Equal( 7, skeleton.Count );
		Assert.Equal( skeleton.IndexOf( "left_hand" ), skeleton.Parents[ skeleton.IndexOf( "left_index1" ) ] );
		Assert.Equal( skeleton.IndexOf( "left_index1" ), skeleton.Parents[ skeleton.IndexOf( "left_index2" ) ] );
		Assert.Equal( skeleton.IndexOf( "right_hand" ), skeleton.Parents[ skeleton.IndexOf( "right_index1" ) ] );
		Assert.Equal( new Vector3( 0f, .05f, 0f ), skeleton.RestPositions[ skeleton.IndexOf( "left_index1" ) ] );
	}

	[Fact]
	public void EachHandShowsThePoseItsStreamNamesAlongTheClip()
	{
		var (layer, body, skeleton, banks) = Setup();
		var clip = Clip( "grip" );
		var expected = new AnimationIndexedPoseBank( banks[ 0 ].Bones.Find( "poses" ) );
		// The left stream holds pose 1 for the first half, then turns to pose 3 by the end; the right one holds pose 4.
		var start = layer.Update( body, clip, 0f, new AnimationParameters(), null, 0f );
		Assert.True( Angle( start.Rotations[ 3 ], expected.Sample( 1 ).Rotations[ 0 ] ) < .01f );
		var end = new HandPoseLayer( skeleton, banks ).Update( body, clip, clip.Duration, new AnimationParameters(), null, 0f );
		Assert.True( Angle( end.Rotations[ 3 ], expected.Sample( 3 ).Rotations[ 0 ] ) < .01f );
		Assert.True( Angle( end.Rotations[ 5 ], expected.Sample( 4 ).Rotations[ 0 ] ) < .01f );
		var middle = new HandPoseLayer( skeleton, banks ).Update( body, clip, clip.Duration * .75f, new AnimationParameters(), null, 0f );
		var between = Angle( middle.Rotations[ 3 ], Quaternion.Identity );
		Assert.InRange( between, Angle( expected.Sample( 1 ).Rotations[ 0 ], Quaternion.Identity ) + 1f,
			Angle( expected.Sample( 3 ).Rotations[ 0 ], Quaternion.Identity ) - 1f );
		// The body bones pass through untouched.
		Assert.Equal( body.Rotations[ 1 ], end.Rotations[ 1 ] );
	}

	[Fact]
	public void AClipWithoutStreamsShowsTheBankReference()
	{
		var (layer, body, _, _) = Setup();
		var pose = layer.Update( body, Clip( "grip", streams: false ), 0f, new AnimationParameters(), null, 0f );
		Assert.True( Angle( pose.Rotations[ 3 ], Quaternion.Identity ) < .01f );
		Assert.True( Angle( pose.Rotations[ 5 ], Quaternion.Identity ) < .01f );
	}

	[Fact]
	public void AChangeOfClipMovesTheFingersWithoutAJump()
	{
		var (layer, body, _, _) = Setup();
		var open = Clip( "open", left: 0, right: 0 );
		var fist = Clip( "fist", left: 4, right: 4 );
		var previous = layer.Update( body, open, 0f, new AnimationParameters(), null, 1f / 60f ).Rotations.ToArray();
		var gap = Angle( previous[ 3 ], layer.Update( body, open, 0f, new AnimationParameters(), null, 1f / 60f ).Rotations[ 3 ] );
		Assert.True( gap < .01f );
		var largest = 0f;
		var first = 0f;
		for ( var frame = 0; frame < 30; frame++ )
		{
			var pose = layer.Update( body, fist, frame / 60f, new AnimationParameters(), null, 1f / 60f );
			var step = Angle( previous[ 3 ], pose.Rotations[ 3 ] );
			if ( frame == 0 )
				first = step;
			largest = MathF.Max( largest, step );
			previous = pose.Rotations.ToArray();
		}
		// Eighty degrees to cover: the first frame starts gently and no frame moves a fifth of the way.
		Assert.True( first < 8f, $"first {first}" );
		Assert.True( largest < 80f / 5f, $"largest {largest}" );
		var settled = Angle( previous[ 3 ], new AnimationIndexedPoseBank( Bank( "left" ).Find( "poses" ) ).Sample( 4 ).Rotations[ 0 ] );
		Assert.True( settled < 1f, $"settled {settled}" );
	}

	[Fact]
	public void BlendsMixTheirClipsStreamsByWeight()
	{
		var (layer, body, skeleton, banks) = Setup();
		var clips = new ClipSet( skeleton, new[] { Clip( "open", left: 0, right: 0 ), Clip( "fist", left: 4, right: 4 ) } );
		clips.AddBlends( new System.Collections.Generic.Dictionary<string, BlendDefinition>
		{
			[ "mix" ] = new BlendDefinition
			{
				Parameter = "grip",
				Children = new() { new BlendChildDefinition { Name = "open", At = 0f }, new BlendChildDefinition { Name = "fist", At = 1f } }
			}
		} );
		var parameters = new AnimationParameters();
		parameters.Set( "grip", .5f );
		var pose = layer.Update( body, clips.Find( "mix" ), 0f, parameters, null, 0f );
		var bank = new AnimationIndexedPoseBank( banks[ 0 ].Bones.Find( "poses" ) );
		var half = Quaternion.Normalize( Quaternion.Lerp( bank.Sample( 0 ).Rotations[ 0 ], bank.Sample( 4 ).Rotations[ 0 ], .5f ) );
		Assert.True( Angle( pose.Rotations[ 3 ], half ) < .5f );
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Clamp( MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ), 0f, 1f ) ) * 180f / MathF.PI;

	private static HandPoseLayer Layer() => Setup().Layer;

	// A root and two hands, the left one turned, with a two-joint finger bank on each hand.
	private static (HandPoseLayer Layer, SkeletonPose Body, ClipSkeleton Skeleton, HandPoseBank[] Banks) Setup()
	{
		var skeleton = new ClipSkeleton( new[] { "root", "left_hand", "right_hand" }, new[] { -1, 0, 0 },
			new[] { Vector3.Zero, new Vector3( 0f, .5f, 1f ), new Vector3( 0f, -.5f, 1f ) },
			Enumerable.Repeat( Quaternion.Identity, 3 ).ToArray() );
		var banks = new[]
		{
			new HandPoseBank( "left_hand", Left, Bank( "left" ), "poses" ),
			new HandPoseBank( "right_hand", Right, Bank( "right" ), "poses" )
		};
		var pose = new SkeletonPose( 3 );
		pose.SetRest( skeleton );
		pose.Rotations[ 1 ] = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, .3f );
		return (new HandPoseLayer( skeleton, banks ), pose, skeleton, banks);
	}

	// A three-bone keyed clip bound to raw poses whose streams pick hand poses: by default the left hand holds pose 1
	// to half way, then turns to pose 3 at the end, and the right hand holds pose 4.
	private static AnimationClip Clip( string name, bool streams = true, int left = -1, int right = -1 )
	{
		var keyed = new AnimationClip( name, 30f, 31, new int[ 3 ], new int[ 3 ], Array.Empty<Quaternion>(), new int[ 3 ], new int[ 3 ],
			Array.Empty<Vector3>() );
		var leftKeys = left >= 0 ? new (short, ushort)[] { (0, (ushort)left), (8191, (ushort)left) }
			: new (short, ushort)[] { (0, 1), (4096, 1), (8191, 3) };
		var rightKeys = new (short, ushort)[] { (0, (ushort)(right >= 0 ? right : 4)), (8191, (ushort)(right >= 0 ? right : 4)) };
		var raw = RawAnimationClipSet.Read( Container( new[] { "root", "left_hand", "right_hand" }, new[] { -1, 0, 0 },
			name, 30f, 31, streams ? new[] { (Left, Right, leftKeys), (Right, Left, rightKeys) } : null, null ) );
		keyed.BindRawPose( raw.CreateReferencedClip( name ) );
		keyed.Looping = false;
		return keyed;
	}

	// A hand bank: two finger bones whose single clip holds one pose per frame, each bending both joints by Bends.
	private static RawAnimationClipSet Bank( string side )
	{
		var rotations = Bends.Select( degrees => Quaternion.CreateFromAxisAngle( Vector3.UnitX, degrees * MathF.PI / 180f ) ).ToArray();
		return RawAnimationClipSet.Read( Container( new[] { $"{side}_index1", $"{side}_index2" }, new[] { -1, 0 },
			"poses", 60f, Bends.Length, null, rotations, new[] { new Vector3( 0f, .05f, 0f ), new Vector3( 0f, .03f, 0f ) } ) );
	}

	// A version 5 raw pose container with one clip. Bones without key rotations hold their reference.
	private static byte[] Container( string[] names, int[] parents, string clip, float fps, int frames,
		(AnimationPoseStreamKey Name, AnimationPoseStreamKey Paired, (short Time, ushort Value)[] Keys)[] streams,
		Quaternion[] rotations, Vector3[] rest = null )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( 5u );
		writer.Write( (ushort)names.Length );
		for ( var bone = 0; bone < names.Length; bone++ )
		{
			Text( writer, names[ bone ] );
			writer.Write( (short)parents[ bone ] );
		}
		writer.Write( (ushort)1 );
		for ( var bone = 0; bone < names.Length; bone++ )
		{
			var position = rest?[ bone ] ?? Vector3.Zero;
			foreach ( var value in new[] { position.X, position.Y, position.Z, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f } )
				writer.Write( value );
		}
		writer.Write( 1u );
		Text( writer, clip );
		writer.Write( fps );
		writer.Write( 1f );
		writer.Write( (ushort)frames );
		writer.Write( (ushort)0 );
		writer.Write( (byte)1 );
		writer.Write( (byte)0 );
		writer.Write( -1 );
		foreach ( var part in new[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f } )
			writer.Write( part );
		writer.Write( (byte)(streams?.Length ?? 0) );
		foreach ( var (name, paired, keys) in streams ?? Array.Empty<(AnimationPoseStreamKey, AnimationPoseStreamKey, (short, ushort)[])>() )
		{
			foreach ( var word in new[] { name.A, name.B, name.C, name.D, name.E, name.F, paired.A, paired.B, paired.C, paired.D, paired.E, paired.F } )
				writer.Write( word );
			writer.Write( (uint)keys.Length );
			foreach ( var (time, value) in keys )
			{
				writer.Write( time );
				writer.Write( value );
			}
		}
		for ( var bone = 0; bone < names.Length; bone++ )
			writer.Write( new byte[] { 0, (byte)(rotations is null ? 0 : 2), 0 } );
		for ( var bone = 0; rotations is not null && bone < names.Length; bone++ )
		{
			foreach ( var rotation in rotations )
			{
				writer.Write( rotation.X );
				writer.Write( rotation.Y );
				writer.Write( rotation.Z );
				writer.Write( rotation.W );
			}
		}
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string value )
	{
		var bytes = Encoding.UTF8.GetBytes( value );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
