using System.Numerics;
using System.Text;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationTests
{
	// root -> hips -> left_leg / right_leg, metres, Z up.
	private static readonly string[] Names = { "root", "hips", "left_leg", "right_leg" };
	private static readonly int[] Parents = { -1, 0, 1, 1 };
	private static readonly Vector3[] Rest = { Vector3.Zero, new( 0f, 0f, 1f ), new( 0f, 0.1f, -0.5f ), new( 0f, -0.1f, -0.5f ) };

	[Fact]
	public void ClipSetRoundTripsTracksAndInterpolates()
	{
		var set = ClipSet.Read( Build( ("swing", 10f, Swing( 11 )) ) );
		var clip = set.Find( "SWING" );
		Assert.Equal( 4, set.Skeleton.Count );
		Assert.Equal( 1f, clip.Duration, 4 );

		var pose = new SkeletonPose( 4 );
		pose.Sample( clip, set.Skeleton, 0.25f, false );
		var expected = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.25f );
		Assert.True( Angle( expected, pose.Rotations[ 2 ] ) < 0.002f );
		Assert.Equal( Rest[ 2 ], pose.Positions[ 2 ] );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 3 ] );
	}

	[Fact]
	public void LoopingWrapsAndOneShotsHold()
	{
		var set = ClipSet.Read( Build( ("swing", 10f, Swing( 11 )) ) );
		var clip = set.Find( "swing" );
		var pose = new SkeletonPose( 4 );
		pose.Sample( clip, set.Skeleton, 1.25f, true );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.25f ), pose.Rotations[ 2 ] ) < 0.002f );
		pose.Sample( clip, set.Skeleton, 3f, false );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 1f ), pose.Rotations[ 2 ] ) < 0.002f );
	}

	[Fact]
	public void GraphPicksFirstApplicableStateAndSubstitutesTags()
	{
		var set = ClipSet.Read( Build( ("idle", 10f, Still( 0f )), ("fast", 10f, Still( 1f )), ("trick_a", 10f, Still( 2f )) ) );
		var graph = new MotionGraph( set, Definition() );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );

		parameters.Set( "airborne", false );
		parameters.Set( "trick", "a" );
		graph.Update( parameters, 0.1f, pose );
		Assert.Equal( "ride", graph.State );

		parameters.Set( "airborne", true );
		graph.Update( parameters, 0f, pose );
		Assert.Equal( "trick", graph.State );
		graph.Update( parameters, 1f, pose );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 2f ), pose.Rotations[ 2 ] ) < 0.002f );

		parameters.Set( "trick", "missing" );
		parameters.Set( "airborne", false );
		graph.Update( parameters, 1f, pose );
		Assert.Equal( "ride", graph.State );
	}

	[Fact]
	public void BlendPointsWeightNeighbouringMotions()
	{
		var set = ClipSet.Read( Build( ("idle", 10f, Still( 0f )), ("fast", 10f, Still( 1f )), ("trick_a", 10f, Still( 2f )) ) );
		var graph = new MotionGraph( set, Definition() );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );
		parameters.Set( "speed", 2.5f );
		graph.Update( parameters, 0.1f, pose );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.5f ), pose.Rotations[ 2 ] ) < 0.002f );
	}

	[Fact]
	public void VariantsRotateOnEachEntry()
	{
		var set = ClipSet.Read( Build( ("land_1", 10f, Still( 0.1f )), ("land_2", 10f, Still( 0.2f )) ) );
		var definition = new AnimationGraphDefinition();
		definition.Motions.Add( Motion( "land_1", ("family", "land") ) );
		definition.Motions.Add( Motion( "land_2", ("family", "land") ) );
		definition.States.Add( State( "land", new[] { ("landing", "==", "1") }, Step( ("family", "land") ) ) );
		definition.States.Add( State( "none", Array.Empty<(string, string, string)>(), Step( ("family", "land") ) ) );
		var graph = new MotionGraph( set, definition );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );
		var seen = new HashSet<float>();
		for ( var i = 0; i < 4; i++ )
		{
			parameters.Set( "landing", i % 2 == 0 );
			graph.Update( parameters, 0.5f, pose );
			if ( graph.State == "land" )
				seen.Add( MathF.Round( 2f * MathF.Atan2( pose.Rotations[ 2 ].Y, pose.Rotations[ 2 ].W ), 2 ) );
		}
		Assert.Equal( 2, seen.Count );
	}

	[Fact]
	public void PreferencesPickTheClosestMotionAndKindIsExact()
	{
		var set = ClipSet.Read( Build( ("push_slow", 10f, Still( 0.1f )), ("push_fast", 10f, Still( 0.2f )), ("push_fast_left", 10f, Still( 0.3f )) ) );
		var definition = new AnimationGraphDefinition();
		definition.Motions.Add( Motion( "push_slow", ("action", "push"), ("speed", "slow"), ("kind", "push-slow") ) );
		definition.Motions.Add( Motion( "push_fast", ("action", "push"), ("speed", "fast"), ("kind", "push-fast") ) );
		definition.Motions.Add( Motion( "push_fast_left", ("action", "push"), ("speed", "fast"), ("lean", "left"), ("kind", "push-fast-left") ) );
		var step = Step( ("action", "$action") );
		step.Prefer = new() { [ "speed" ] = "$speed", [ "lean" ] = "$lean", [ "kind" ] = "$kind" };
		definition.States.Add( State( "any", Array.Empty<(string, string, string)>(), step ) );
		var graph = new MotionGraph( set, definition );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );

		parameters.Set( "action", "push" );
		parameters.Set( "speed", "fast" );
		parameters.Set( "lean", "left" );
		graph.Update( parameters, 0f, pose );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.3f ), pose.Rotations[ 2 ] ) < 0.002f );

		parameters.Set( "kind", "push-slow" );
		graph.Update( parameters, 0f, pose );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.1f ), pose.Rotations[ 2 ] ) < 0.002f );
	}

	[Fact]
	public void UnrequestedQualifiersOnlyBreakTies()
	{
		var set = ClipSet.Read( Build( ("plain", 10f, Still( 0.1f )), ("detailed", 10f, Still( 0.2f )) ) );
		var definition = new AnimationGraphDefinition();
		definition.Motions.Add( Motion( "plain", ("action", "run"), ("side", "front"), ("speed", "fast") ) );
		definition.Motions.Add( Motion( "detailed", ("action", "run"), ("side", "front"), ("speed", "fast"), ("height", "thin") ) );
		var step = Step( ("action", "$action") );
		step.Prefer = new() { [ "side" ] = "$side", [ "speed" ] = "$speed", [ "height" ] = "$height" };
		definition.States.Add( State( "any", Array.Empty<(string, string, string)>(), step ) );
		var graph = new MotionGraph( set, definition );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );
		parameters.Set( "action", "run" );
		graph.Update( parameters, 0f, pose );
		Assert.Equal( "plain", graph.ClipName );
	}

	[Fact]
	public void IntroThenCycleAndExitBeforeTheNextState()
	{
		var set = ClipSet.Read( Build( ("in", 10f, Swing( 6 )), ("loop", 10f, Still( 1f )), ("out", 10f, Still( 1.5f )), ("idle", 10f, Still( 0f )) ) );
		var definition = new AnimationGraphDefinition();
		definition.Motions.Add( Motion( "in", ("action", "push"), ("phase", "into") ) );
		definition.Motions.Add( Motion( "loop", ("action", "push"), ("phase", "cycle") ) );
		definition.Motions.Add( Motion( "out", ("action", "push"), ("phase", "out") ) );
		definition.Motions.Add( Motion( "idle", ("action", "idle"), ("phase", "cycle") ) );
		var intro = Step( ("action", "$action"), ("phase", "into") );
		intro.Optional = true;
		var push = State( "cycle", Array.Empty<(string, string, string)>(), intro );
		push.Steps.Add( Step( ("action", "$action"), ("phase", "cycle") ) );
		push.Loop = true;
		push.Exit = Step( ("action", "$action"), ("phase", "out") );
		push.Exit.Optional = true;
		definition.States.Add( push );
		var graph = new MotionGraph( set, definition );
		var parameters = new AnimationParameters();
		var pose = new SkeletonPose( 4 );
		float Angle2() => 2f * MathF.Atan2( pose.Rotations[ 2 ].Y, pose.Rotations[ 2 ].W );

		parameters.Set( "action", "push" );
		graph.Update( parameters, 0.25f, pose );
		Assert.InRange( Angle2(), 0.4f, 0.6f );
		graph.Update( parameters, 0.5f, pose );
		graph.Update( parameters, 0.5f, pose );
		Assert.Equal( 1f, Angle2(), 2 );

		parameters.Set( "action", "idle" );
		graph.Update( parameters, 0.05f, pose );
		Assert.Equal( 1.5f, Angle2(), 2 );
		graph.Update( parameters, 0.2f, pose );
		Assert.Equal( 0f, Angle2(), 2 );
	}

	[Fact]
	public void RetargetingOntoTheSameSkeletonReproducesTheSourcePose()
	{
		var set = ClipSet.Read( Build( ("swing", 10f, Swing( 11 )) ) );
		var bind = World( Rest, Enumerable.Repeat( Quaternion.Identity, 4 ).ToArray() );
		var map = Names.ToDictionary( name => name, name => name );
		var retargeter = new PoseRetargeter( set.Skeleton, Names, Parents, bind.Positions, bind.Rotations, map );
		var pose = new SkeletonPose( 4 );
		pose.Sample( set.Find( "swing" ), set.Skeleton, 0.5f, false );
		var positions = new Vector3[ 4 ];
		var rotations = new Quaternion[ 4 ];
		retargeter.Apply( pose, false, positions, rotations );
		var expected = World( pose.Positions, pose.Rotations );
		for ( var i = 0; i < 4; i++ )
			Assert.True( Vector3.Distance( expected.Positions[ i ], positions[ i ] ) < 0.0001f, $"bone {i}" );
		Assert.Equal( 1f, retargeter.Scale, 4 );
	}

	[Fact]
	public void MirroringSwapsSidesAndReflectsAcrossTheCentreline()
	{
		var set = ClipSet.Read( Build( ("swing", 10f, Swing( 11 )) ) );
		var bind = World( Rest, Enumerable.Repeat( Quaternion.Identity, 4 ).ToArray() );
		var map = Names.ToDictionary( name => name, name => name );
		var retargeter = new PoseRetargeter( set.Skeleton, Names, Parents, bind.Positions, bind.Rotations, map );
		var pose = new SkeletonPose( 4 );
		pose.Sample( set.Find( "swing" ), set.Skeleton, 1f, false );
		var positions = new Vector3[ 4 ];
		var rotations = new Quaternion[ 4 ];
		retargeter.Apply( pose, true, positions, rotations );
		// The swinging left leg's motion now belongs to the right leg; the left leg stays at rest.
		Assert.True( Angle( Quaternion.Identity, rotations[ 2 ] ) < 0.002f );
		Assert.True( Angle( Quaternion.CreateFromAxisAngle( Vector3.UnitY, 1f ), rotations[ 3 ] ) < 0.002f );
	}

	private static AnimationGraphDefinition Definition()
	{
		var definition = new AnimationGraphDefinition();
		definition.Motions.Add( Motion( "idle", ("family", "ride"), ("pace", "slow") ) );
		definition.Motions.Add( Motion( "fast", ("family", "ride"), ("pace", "fast") ) );
		definition.Motions.Add( Motion( "trick_a", ("family", "trick"), ("trick", "a") ) );
		definition.States.Add( State( "trick", new[] { ("airborne", "==", "1") }, Step( ("family", "trick"), ("trick", "$trick") ) ) );
		var ride = Step( ("family", "ride") );
		ride.BlendParameter = "speed";
		ride.BlendTag = "pace";
		ride.BlendPoints = new() { [ "slow" ] = 0f, [ "fast" ] = 5f };
		var state = State( "ride", Array.Empty<(string, string, string)>(), ride );
		state.Loop = true;
		definition.States.Add( state );
		return definition;
	}

	private static AnimationMotionDefinition Motion( string clip, params (string Key, string Value)[] tags ) =>
		new() { Clip = clip, Tags = tags.ToDictionary( tag => tag.Key, tag => tag.Value ) };

	private static AnimationStepDefinition Step( params (string Key, string Value)[] select ) =>
		new() { Select = select.ToDictionary( tag => tag.Key, tag => tag.Value ) };

	private static AnimationStateDefinition State( string name, (string, string, string)[] when, AnimationStepDefinition step ) => new()
	{
		Name = name,
		BlendTime = 0f,
		When = when.Select( c => new AnimationConditionDefinition { Parameter = c.Item1, Op = c.Item2, Value = c.Item3 } ).ToList(),
		Steps = new() { step }
	};

	// Left leg swings about Y from 0 to 1 radian across the frames.
	private static Quaternion[] Swing( int frames ) => Enumerable.Range( 0, frames )
		.Select( f => Quaternion.CreateFromAxisAngle( Vector3.UnitY, f / (float)(frames - 1) ) ).ToArray();

	private static Quaternion[] Still( float angle ) => new[] { Quaternion.CreateFromAxisAngle( Vector3.UnitY, angle ) };

	private static byte[] Build( params (string Name, float Rate, Quaternion[] Leg)[] clips )
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
		foreach ( var (name, rate, leg) in clips )
		{
			Text( writer, name );
			writer.Write( rate );
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

	private static (Vector3[] Positions, Quaternion[] Rotations) World( Vector3[] local, Quaternion[] rotations )
	{
		var positions = new Vector3[ 4 ];
		var world = new Quaternion[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			if ( Parents[ i ] < 0 )
			{
				world[ i ] = Quaternion.Identity;
				continue;
			}
			positions[ i ] = positions[ Parents[ i ] ] + Vector3.Transform( local[ i ], world[ Parents[ i ] ] );
			world[ i ] = world[ Parents[ i ] ] * rotations[ i ];
		}
		return (positions, world);
	}

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Clamp( MathF.Abs( Quaternion.Dot( Quaternion.Normalize( a ), Quaternion.Normalize( b ) ) ), 0f, 1f ) );
}
