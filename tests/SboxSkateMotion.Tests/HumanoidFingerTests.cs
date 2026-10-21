using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HumanoidFingerTests
{
	// One humanoid, metres, facing +X with +Y on its left and Z up, arms straight out: role, parent role, position.
	private static readonly (string Role, string Parent, Vector3 Position)[] Body =
	{
		("hips", null, new( 0f, 0f, 1f )),
		("spine", "hips", new( 0f, 0f, 1.15f )),
		("spine3", "spine", new( 0f, 0f, 1.4f )),
		("neck", "spine3", new( 0f, 0f, 1.55f )),
		("head", "neck", new( 0f, 0f, 1.65f )),
		("left_shoulder", "spine3", new( 0f, .05f, 1.5f )),
		("left_arm", "left_shoulder", new( 0f, .18f, 1.5f )),
		("left_forearm", "left_arm", new( 0f, .45f, 1.5f )),
		("left_hand", "left_forearm", new( 0f, .7f, 1.5f )),
		("right_shoulder", "spine3", new( 0f, -.05f, 1.5f )),
		("right_arm", "right_shoulder", new( 0f, -.18f, 1.5f )),
		("right_forearm", "right_arm", new( 0f, -.45f, 1.5f )),
		("right_hand", "right_forearm", new( 0f, -.7f, 1.5f )),
		("left_upleg", "hips", new( 0f, .1f, .95f )),
		("left_leg", "left_upleg", new( 0f, .1f, .5f )),
		("left_foot", "left_leg", new( 0f, .1f, .08f )),
		("left_toe", "left_foot", new( .12f, .1f, .02f )),
		("right_upleg", "hips", new( 0f, -.1f, .95f )),
		("right_leg", "right_upleg", new( 0f, -.1f, .5f )),
		("right_foot", "right_leg", new( 0f, -.1f, .08f )),
		("right_toe", "right_foot", new( .12f, -.1f, .02f )),
	};

	// Where each finger's knuckle sits across the left hand (forward offset) and the thumb, which points forward.
	private static readonly Dictionary<string, float> Across = new() { [ "thumb" ] = .035f, [ "index" ] = .03f, [ "middle" ] = .01f,
		[ "ring" ] = -.01f, [ "pinky" ] = -.03f };

	[Theory]
	[InlineData( "citizen", true, false, false )]
	[InlineData( "mixamo", false, true, true )]
	[InlineData( "unreal", true, false, true )]
	[InlineData( "biped", false, true, true )]
	[InlineData( "unnamed", false, false, true )]
	public void FindsEveryFingerJointForCommonRigConventions( string convention, bool bases, bool ends, bool pinky )
	{
		var rig = Rig( convention, bases, ends, pinky );
		var bones = new HumanoidBones( rig.Names, rig.Parents, rig.Positions );
		Assert.True( bones.Complete );
		foreach ( var (role, index) in rig.Roles.Select( ( role, index ) => (role, index) ) )
		{
			if ( role.EndsWith( "_end" ) )
				Assert.DoesNotContain( rig.Names[ index ], bones.Map().Keys );
			else if ( HumanoidBones.Fingers.Any( role.Contains ) )
				Assert.Equal( index, bones[ role ] );
		}
		Assert.Equal( pinky ? 2 * 15 : 2 * 12, HumanoidBones.Fingers.Sum( finger =>
			new[] { "left", "right" }.Sum( side => new[] { "1", "2", "3" }.Count( joint => bones[ $"{side}_{finger}{joint}" ] >= 0 ) ) ) );
		// The arm roles are unchanged by the fingers below the hand.
		Assert.Equal( Array.IndexOf( rig.Roles, "left_hand" ), bones[ "left_hand" ] );
		Assert.Equal( Array.IndexOf( rig.Roles, "right_forearm" ), bones[ "right_forearm" ] );
	}

	[Fact]
	public void HelpersHangingFromTheHandAreNotFingers()
	{
		var rig = Rig( "citizen", true, false, false );
		var names = rig.Names.ToList();
		var parents = rig.Parents.ToList();
		var positions = rig.Positions.ToList();
		var hand = Array.IndexOf( rig.Roles, "left_hand" );
		foreach ( var (name, parent) in new[] { ("hold_L", hand), ("hand_L_ik_target", hand), ("weapon_attach_L", hand), ("weapon_attach_tip_L", -2) } )
		{
			parents.Add( parent == -2 ? names.Count - 1 : parent );
			names.Add( name );
			positions.Add( positions[ hand ] + new Vector3( .05f, .02f, -.04f ) );
		}
		var bones = new HumanoidBones( names.ToArray(), parents.ToArray(), positions.ToArray() );
		var roles = bones.Map();
		foreach ( var helper in new[] { "hold_L", "hand_L_ik_target", "weapon_attach_L", "weapon_attach_tip_L" } )
			Assert.DoesNotContain( helper, roles.Keys );
		Assert.Equal( "left_index1", roles[ "finger_index_0_L" ] );
	}

	[Fact]
	public void FingersRetargetTheirCurlWithoutTurningTheHand()
	{
		// Source: canonical names, no finger bases; target: a citizen-style rig whose fingers start at metacarpals.
		var source = Rig( "source", false, false, true );
		var local = source.Positions.Select( ( position, i ) => source.Parents[ i ] < 0 ? position : position - source.Positions[ source.Parents[ i ] ] ).ToArray();
		var skeleton = new ClipSkeleton( source.Roles, source.Parents, local, Enumerable.Repeat( Quaternion.Identity, source.Roles.Length ).ToArray() );
		var target = Rig( "citizen", true, false, false );
		var bones = new HumanoidBones( target.Names, target.Parents, target.Positions );
		var rotations = Enumerable.Repeat( Quaternion.Identity, target.Names.Length ).ToArray();
		var withFingers = new PoseRetargeter( skeleton, target.Names, target.Parents, target.Positions, rotations, bones.Map(), bones.Facing );
		var bodyOnly = new PoseRetargeter( skeleton, target.Names, target.Parents, target.Positions, rotations,
			bones.Map().Where( pair => !HumanoidBones.Fingers.Any( pair.Value.Contains ) ).ToDictionary( pair => pair.Key, pair => pair.Value ), bones.Facing );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		// Turn the left hand and curl its index finger 40 degrees at each joint about the knuckle line (forward).
		pose.Rotations[ skeleton.IndexOf( "left_hand" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitY, .4f );
		foreach ( var joint in new[] { "left_index1", "left_index2", "left_index3" } )
			pose.Rotations[ skeleton.IndexOf( joint ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitX, 40f * MathF.PI / 180f );
		var count = target.Names.Length;
		var (positions, turned) = (new Vector3[ count ], new Quaternion[ count ]);
		var (bodyPositions, bodyTurned) = (new Vector3[ count ], new Quaternion[ count ]);
		withFingers.Apply( pose, false, positions, turned );
		bodyOnly.Apply( pose, false, bodyPositions, bodyTurned );
		var hand = Array.IndexOf( target.Names, "hand_L" );
		Assert.True( Quaternion.Dot( turned[ hand ], bodyTurned[ hand ] ) > .99999f );
		Assert.True( Vector3.Distance( positions[ hand ], bodyPositions[ hand ] ) < 1e-4f );
		float Bend( string a, string b, string c ) => Degrees( positions[ Array.IndexOf( target.Names, b ) ] - positions[ Array.IndexOf( target.Names, a ) ],
			positions[ Array.IndexOf( target.Names, c ) ] - positions[ Array.IndexOf( target.Names, b ) ] );
		// Each joint bends as the source's does; the middle finger, left straight, stays straight.
		Assert.InRange( Bend( "finger_index_0_L", "finger_index_1_L", "finger_index_2_L" ), 35f, 45f );
		Assert.True( Bend( "finger_middle_0_L", "finger_middle_1_L", "finger_middle_2_L" ) < 2f );
		// Without finger bones in the source the fingers keep following the hand, straight.
		Assert.True( Degrees( bodyPositions[ Array.IndexOf( target.Names, "finger_index_1_L" ) ] - bodyPositions[ Array.IndexOf( target.Names, "finger_index_0_L" ) ],
			bodyPositions[ Array.IndexOf( target.Names, "finger_index_2_L" ) ] - bodyPositions[ Array.IndexOf( target.Names, "finger_index_1_L" ) ] ) < 2f );
	}

	[Fact]
	public void AHandBuiltBentOnItsWristLiesAsTheSourceHandDoes()
	{
		var source = Rig( "source", false, false, true );
		var local = source.Positions.Select( ( position, i ) => source.Parents[ i ] < 0 ? position : position - source.Positions[ source.Parents[ i ] ] ).ToArray();
		var skeleton = new ClipSkeleton( source.Roles, source.Parents, local, Enumerable.Repeat( Quaternion.Identity, source.Roles.Length ).ToArray() );
		// The target's left hand is built bent 30 degrees down and rolled 20 degrees on its wrist: its fingers follow.
		var target = Rig( "citizen", true, false, false );
		var hand = Array.IndexOf( target.Roles, "left_hand" );
		var bent = Quaternion.CreateFromAxisAngle( Vector3.UnitY, 20f * MathF.PI / 180f ) * Quaternion.CreateFromAxisAngle( Vector3.UnitX, -30f * MathF.PI / 180f );
		var positions = target.Positions.ToArray();
		for ( var i = 0; i < positions.Length; i++ )
		{
			if ( target.Roles[ i ].StartsWith( "left_" ) && HumanoidBones.Fingers.Any( target.Roles[ i ].Contains ) )
				positions[ i ] = positions[ hand ] + Vector3.Transform( positions[ i ] - positions[ hand ], bent );
		}
		var bones = new HumanoidBones( target.Names, target.Parents, positions );
		var retargeter = new PoseRetargeter( skeleton, target.Names, target.Parents, positions,
			Enumerable.Repeat( Quaternion.Identity, target.Names.Length ).ToArray(), bones.Map(), bones.Facing );
		var pose = new SkeletonPose( skeleton.Count );
		pose.SetRest( skeleton );
		var (shown, turned) = (new Vector3[ target.Names.Length ], new Quaternion[ target.Names.Length ]);
		retargeter.Apply( pose, false, shown, turned );
		Vector3 At( string role ) => shown[ Array.IndexOf( target.Roles, role ) ];
		Vector3 From( string role ) => source.Positions[ Array.IndexOf( source.Roles, role ) ];
		// From the wrist to the knuckles and along the knuckle line, the target hand now lies as the source's.
		Assert.True( Degrees( At( "left_middle1" ) - At( "left_hand" ), From( "left_middle1" ) - From( "left_hand" ) ) < 3f );
		Assert.True( Degrees( At( "left_ring1" ) - At( "left_index1" ), From( "left_ring1" ) - From( "left_index1" ) ) < 3f );
		// The right hand, built straight, is unchanged.
		Assert.True( Degrees( At( "right_ring1" ) - At( "right_index1" ), From( "right_ring1" ) - From( "right_index1" ) ) < 3f );
	}

	private static float Degrees( Vector3 a, Vector3 b ) =>
		MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( a ), Vector3.Normalize( b ) ), -1f, 1f ) ) * 180f / MathF.PI;

	// The body under a naming convention, with three-joint fingers on both hands (optionally a metacarpal base
	// before each, an end marker after each, and a pinky).
	private static (string[] Names, string[] Roles, int[] Parents, Vector3[] Positions) Rig( string convention, bool bases, bool ends,
		bool pinky )
	{
		var roles = new List<string>( Body.Select( bone => bone.Role ) );
		var parents = new List<int>( Body.Select( bone => bone.Parent is null ? -1 : Array.FindIndex( Body, other => other.Role == bone.Parent ) ) );
		var positions = new List<Vector3>( Body.Select( bone => bone.Position ) );
		foreach ( var side in new[] { "left", "right" } )
		{
			var hand = roles.IndexOf( $"{side}_hand" );
			var outward = side == "left" ? 1f : -1f;
			foreach ( var finger in HumanoidBones.Fingers )
			{
				if ( finger == "pinky" && !pinky )
					continue;
				var parent = hand;
				var at = positions[ hand ];
				if ( bases && finger != "thumb" )
				{
					at += new Vector3( Across[ finger ] * .5f, .02f * outward, 0f );
					Add( $"{side}_{finger}_base", parent, at );
					parent = roles.Count - 1;
				}
				var step = finger == "thumb" ? new Vector3( .025f, .01f * outward, -.005f ) : new Vector3( 0f, .03f * outward, -.004f );
				at = finger == "thumb" ? positions[ hand ] + new Vector3( Across[ finger ], .02f * outward, -.01f )
					: positions[ hand ] + new Vector3( Across[ finger ], .09f * outward, 0f );
				for ( var joint = 1; joint <= 3; joint++ )
				{
					Add( $"{side}_{finger}{joint}", parent, at );
					parent = roles.Count - 1;
					at += step;
				}
				if ( ends )
					Add( $"{side}_{finger}_end", parent, at );
			}
		}
		var names = roles.Select( ( role, index ) => Name( convention, role, index ) ).ToArray();
		return (names, roles.ToArray(), parents.ToArray(), positions.ToArray());

		void Add( string role, int parent, Vector3 position )
		{
			roles.Add( role );
			parents.Add( parent );
			positions.Add( position );
		}
	}

	private static string Name( string convention, string role, int index )
	{
		if ( convention == "unnamed" )
			return $"bone_{index:00}";
		if ( convention == "source" )
			return role;
		var side = role.StartsWith( "left_" ) ? "left" : role.StartsWith( "right_" ) ? "right" : null;
		var part = side is null ? role : role[ (side.Length + 1).. ];
		var finger = HumanoidBones.Fingers.FirstOrDefault( part.StartsWith );
		if ( finger is null )
		{
			var body = convention switch
			{
				"citizen" => new Dictionary<string, string> { [ "hips" ] = "pelvis", [ "spine" ] = "spine_0", [ "spine3" ] = "spine_2", [ "neck" ] = "neck_0",
					[ "head" ] = "head", [ "shoulder" ] = "clavicle_{0}", [ "arm" ] = "arm_upper_{0}", [ "forearm" ] = "arm_lower_{0}", [ "hand" ] = "hand_{0}",
					[ "upleg" ] = "leg_upper_{0}", [ "leg" ] = "leg_lower_{0}", [ "foot" ] = "ankle_{0}", [ "toe" ] = "ball_{0}" },
				"mixamo" => new Dictionary<string, string> { [ "hips" ] = "mixamorig:Hips", [ "spine" ] = "mixamorig:Spine", [ "spine3" ] = "mixamorig:Spine2",
					[ "neck" ] = "mixamorig:Neck", [ "head" ] = "mixamorig:Head", [ "shoulder" ] = "mixamorig:{1}Shoulder", [ "arm" ] = "mixamorig:{1}Arm",
					[ "forearm" ] = "mixamorig:{1}ForeArm", [ "hand" ] = "mixamorig:{1}Hand", [ "upleg" ] = "mixamorig:{1}UpLeg", [ "leg" ] = "mixamorig:{1}Leg",
					[ "foot" ] = "mixamorig:{1}Foot", [ "toe" ] = "mixamorig:{1}ToeBase" },
				"unreal" => new Dictionary<string, string> { [ "hips" ] = "pelvis", [ "spine" ] = "spine_01", [ "spine3" ] = "spine_03", [ "neck" ] = "neck_01",
					[ "head" ] = "head", [ "shoulder" ] = "clavicle_{2}", [ "arm" ] = "upperarm_{2}", [ "forearm" ] = "lowerarm_{2}", [ "hand" ] = "hand_{2}",
					[ "upleg" ] = "thigh_{2}", [ "leg" ] = "calf_{2}", [ "foot" ] = "foot_{2}", [ "toe" ] = "ball_{2}" },
				_ => new Dictionary<string, string> { [ "hips" ] = "Bip01 Pelvis", [ "spine" ] = "Bip01 Spine", [ "spine3" ] = "Bip01 Spine2", [ "neck" ] = "Bip01 Neck",
					[ "head" ] = "Bip01 Head", [ "shoulder" ] = "Bip01 {3} Clavicle", [ "arm" ] = "Bip01 {3} UpperArm", [ "forearm" ] = "Bip01 {3} Forearm",
					[ "hand" ] = "Bip01 {3} Hand", [ "upleg" ] = "Bip01 {3} Thigh", [ "leg" ] = "Bip01 {3} Calf", [ "foot" ] = "Bip01 {3} Foot", [ "toe" ] = "Bip01 {3} Toe0" },
			};
			return string.Format( body[ part ], side == "left" ? "L" : "R", side == "left" ? "Left" : "Right", side == "left" ? "l" : "r", side == "left" ? "L" : "R" );
		}
		var joint = part[ finger.Length.. ];
		var number = Array.IndexOf( HumanoidBones.Fingers, finger );
		var s = side == "left" ? "L" : "R";
		return convention switch
		{
			"citizen" => joint == "_base" ? $"finger_{finger}_meta_{s}" : joint == "_end" ? $"finger_{finger}_end_{s}" : $"finger_{finger}_{int.Parse( joint ) - 1}_{s}",
			"mixamo" => $"mixamorig:{(side == "left" ? "Left" : "Right")}Hand{char.ToUpper( finger[ 0 ] )}{finger[ 1.. ]}{(joint == "_end" ? "4" : joint == "_base" ? "0" : joint)}",
			"unreal" => joint == "_base" ? $"{finger}_metacarpal_{s.ToLower()}" : joint == "_end" ? $"{finger}_end_{s.ToLower()}" : $"{finger}_0{joint}_{s.ToLower()}",
			_ => $"Bip01 {s} Finger{number}{(joint == "_end" ? "Nub" : joint == "1" ? "" : (int.Parse( joint ) - 1).ToString())}"
		};
	}
}
