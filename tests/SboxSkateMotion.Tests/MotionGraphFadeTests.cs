using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class MotionGraphFadeTests
{
	// root -> hips -> left_leg / right_leg, metres, Z up.
	private static readonly string[] Names = { "root", "hips", "left_leg", "right_leg" };
	private static readonly int[] Parents = { -1, 0, 1, 1 };
	private static readonly Vector3[] Rest = { Vector3.Zero, new( 0f, 0f, 1f ), new( 0f, 0.1f, -0.5f ), new( 0f, -0.1f, -0.5f ) };

	private const float Step = 1f / 90f;
	private const float Blend = 0.2f;

	[Fact]
	public void AFadeStartsAndEndsAtTheMotionsOwnSpeed()
	{
		// Standing still in one pose, then asked for another a radian away over a 0.2 s fade: the leg sets off and comes to
		// rest gently. At a weight going straight from 0 to 1 it moved at 5 rad/s from the fade's first frame to its last
		// and then stopped dead.
		var (graph, parameters, pose) = Graph();
		parameters.Set( "action", "wave" );
		for ( var frame = 0; frame < 30; frame++ )
			graph.Update( parameters, Step, pose );
		parameters.Set( "action", "rise" );
		var angles = new System.Collections.Generic.List<float> { Leg( pose ) };
		for ( var frame = 0; frame < 30; frame++ )
		{
			graph.Update( parameters, Step, pose );
			angles.Add( Leg( pose ) );
		}
		var straight = 1f / Blend;
		var first = (angles[ 1 ] - angles[ 0 ]) / Step;
		var last = (angles[ 18 ] - angles[ 17 ]) / Step;
		var after = (angles[ 19 ] - angles[ 18 ]) / Step;
		Assert.True( first < straight * 0.2f, $"{first:F2} rad/s in the fade's first frame" );
		Assert.True( last < straight * 0.2f, $"{last:F2} rad/s in the fade's last frame" );
		Assert.Equal( 0f, after, 3 );
		Assert.Equal( 1f, angles[ ^1 ], 3 );
	}

	[Fact]
	public void AGraphStartedAfreshEntersItsNextStateAtOnce()
	{
		// Waving, then not run for a while (another graph shown) and asked to get up: started afresh, the get-up's pose is
		// shown from its first frame, not faded from the wave long gone (which swung a get-up's hips out and back).
		var (graph, parameters, pose) = Graph();
		parameters.Set( "action", "wave" );
		for ( var frame = 0; frame < 30; frame++ )
			graph.Update( parameters, Step, pose );
		graph.Forget();
		parameters.Set( "action", "rise" );
		graph.Update( parameters, Step, pose );
		Assert.Equal( "rise", graph.State );
		Assert.Equal( 1f, Leg( pose ), 3 );
	}

	[Fact]
	public void LeftAloneTheGraphFadesFromWhereItWas()
	{
		// Without starting afresh, a change still fades from the pose the graph showed.
		var (graph, parameters, pose) = Graph();
		parameters.Set( "action", "wave" );
		for ( var frame = 0; frame < 30; frame++ )
			graph.Update( parameters, Step, pose );
		parameters.Set( "action", "rise" );
		graph.Update( parameters, Step, pose );
		Assert.InRange( Leg( pose ), 0f, 0.05f );
	}

	// Two still poses of the left leg, 0 and 1 radian about Y, each a state of its own faded into over Blend.
	private static (MotionGraph Graph, AnimationParameters Parameters, SkeletonPose Pose) Graph()
	{
		var set = ClipSet.Read( Build( ("wave", Still( 0f )), ("rise", Still( 1f )) ) );
		var definition = new AnimationGraphDefinition();
		foreach ( var name in new[] { "wave", "rise" } )
		{
			definition.Motions.Add( new AnimationMotionDefinition { Clip = name, Tags = new() { [ "action" ] = name } } );
			definition.States.Add( new AnimationStateDefinition
			{
				Name = name,
				BlendTime = Blend,
				Loop = true,
				When = new() { new AnimationConditionDefinition { Parameter = "action", Op = "==", Value = name } },
				Steps = new() { new AnimationStepDefinition { Select = new() { [ "action" ] = name } } }
			} );
		}
		return (new MotionGraph( set, definition ), new AnimationParameters(), new SkeletonPose( Names.Length ));
	}

	private static float Leg( SkeletonPose pose ) => 2f * MathF.Atan2( pose.Rotations[ 2 ].Y, pose.Rotations[ 2 ].W );

	private static Quaternion[] Still( float angle ) => new[] { Quaternion.CreateFromAxisAngle( Vector3.UnitY, angle ) };

	// A clip set as ClipSet.Read takes it (as AnimationTests builds them): the skeleton, then each clip's frames of the left
	// leg's turn at 10 frames a second.
	private static byte[] Build( params (string Name, Quaternion[] Leg)[] clips )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SMAN" ) );
		writer.Write( 1u );
		writer.Write( (ushort)Names.Length );
		for ( var i = 0; i < Names.Length; i++ )
		{
			Text( writer, Names[ i ] );
			writer.Write( (short)Parents[ i ] );
			writer.Write( Rest[ i ].X ); writer.Write( Rest[ i ].Y ); writer.Write( Rest[ i ].Z );
			writer.Write( 0f ); writer.Write( 0f ); writer.Write( 0f ); writer.Write( 1f );
		}
		writer.Write( (uint)clips.Length );
		foreach ( var (name, leg) in clips )
		{
			Text( writer, name );
			writer.Write( 10f );
			writer.Write( (ushort)leg.Length );
			for ( var bone = 0; bone < Names.Length; bone++ )
			{
				writer.Write( bone == 2 ? (byte)(leg.Length == 1 ? 1 : 2) : (byte)0 );
				writer.Write( (byte)0 );
			}
			foreach ( var q in leg )
			{
				writer.Write( (short)MathF.Round( q.X * 32767f ) ); writer.Write( (short)MathF.Round( q.Y * 32767f ) );
				writer.Write( (short)MathF.Round( q.Z * 32767f ) ); writer.Write( (short)MathF.Round( q.W * 32767f ) );
			}
		}
		return stream.ToArray();
	}

	private static void Text( BinaryWriter writer, string text )
	{
		var bytes = Encoding.UTF8.GetBytes( text );
		writer.Write( (byte)bytes.Length );
		writer.Write( bytes );
	}
}
