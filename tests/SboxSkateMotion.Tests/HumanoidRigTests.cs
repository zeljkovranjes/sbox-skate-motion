using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HumanoidRigTests
{
	// One humanoid, metres, facing +X with +Y on its left and Z up: role, parent role, model-space rest position.
	private static readonly (string Role, string Parent, Vector3 Position)[] Body =
	{
		("root", null, Vector3.Zero),
		("hips", "root", new( 0f, 0f, 1f )),
		("spine", "hips", new( 0f, 0f, 1.1f )),
		("spine2", "spine", new( 0f, 0f, 1.25f )),
		("spine3", "spine2", new( 0f, 0f, 1.4f )),
		("neck", "spine3", new( 0f, 0f, 1.55f )),
		("head", "neck", new( 0f, 0f, 1.65f )),
		("head_end", "head", new( 0f, 0f, 1.85f )),
		("left_shoulder", "spine3", new( 0f, 0.05f, 1.5f )),
		("left_arm", "left_shoulder", new( 0f, 0.18f, 1.5f )),
		("left_forearm", "left_arm", new( 0f, 0.45f, 1.5f )),
		("left_hand", "left_forearm", new( 0f, 0.7f, 1.5f )),
		("left_index", "left_hand", new( 0.02f, 0.78f, 1.5f )),
		("left_index_end", "left_index", new( 0.02f, 0.84f, 1.5f )),
		("left_thumb", "left_hand", new( 0.04f, 0.74f, 1.5f )),
		("left_thumb_end", "left_thumb", new( 0.07f, 0.78f, 1.5f )),
		("right_shoulder", "spine3", new( 0f, -0.05f, 1.5f )),
		("right_arm", "right_shoulder", new( 0f, -0.18f, 1.5f )),
		("right_forearm", "right_arm", new( 0f, -0.45f, 1.5f )),
		("right_hand", "right_forearm", new( 0f, -0.7f, 1.5f )),
		("right_index", "right_hand", new( 0.02f, -0.78f, 1.5f )),
		("right_index_end", "right_index", new( 0.02f, -0.84f, 1.5f )),
		("right_thumb", "right_hand", new( 0.04f, -0.74f, 1.5f )),
		("right_thumb_end", "right_thumb", new( 0.07f, -0.78f, 1.5f )),
		("left_upleg", "hips", new( 0f, 0.1f, 0.95f )),
		("left_leg", "left_upleg", new( 0f, 0.1f, 0.5f )),
		("left_foot", "left_leg", new( 0f, 0.1f, 0.08f )),
		("left_toe", "left_foot", new( 0.12f, 0.1f, 0.02f )),
		("left_toe_end", "left_toe", new( 0.18f, 0.1f, 0.02f )),
		("right_upleg", "hips", new( 0f, -0.1f, 0.95f )),
		("right_leg", "right_upleg", new( 0f, -0.1f, 0.5f )),
		("right_foot", "right_leg", new( 0f, -0.1f, 0.08f )),
		("right_toe", "right_foot", new( 0.12f, -0.1f, 0.02f )),
		("right_toe_end", "right_toe", new( 0.18f, -0.1f, 0.02f )),
	};

	private static readonly string[] Roles =
	{
		"hips", "spine", "spine3", "neck", "head", "left_shoulder", "left_arm", "left_forearm", "left_hand",
		"right_shoulder", "right_arm", "right_forearm", "right_hand", "left_upleg", "left_leg", "left_foot", "left_toe",
		"right_upleg", "right_leg", "right_foot", "right_toe"
	};

	private static readonly Dictionary<string, string> Citizen = new()
	{
		[ "hips" ] = "pelvis", [ "spine" ] = "spine_0", [ "spine2" ] = "spine_1", [ "spine3" ] = "spine_2", [ "neck" ] = "neck_0",
		[ "head" ] = "head", [ "left_shoulder" ] = "clavicle_L", [ "left_arm" ] = "arm_upper_L", [ "left_forearm" ] = "arm_lower_L",
		[ "left_hand" ] = "hand_L", [ "left_index" ] = "finger_index_0_L", [ "left_thumb" ] = "finger_thumb_0_L",
		[ "left_upleg" ] = "leg_upper_L", [ "left_leg" ] = "leg_lower_L", [ "left_foot" ] = "ankle_L", [ "left_toe" ] = "ball_L",
	};

	private static readonly Dictionary<string, string> Mixamo = new()
	{
		[ "hips" ] = "Hips", [ "spine" ] = "Spine", [ "spine2" ] = "Spine1", [ "spine3" ] = "Spine2", [ "neck" ] = "Neck",
		[ "head" ] = "Head", [ "head_end" ] = "HeadTop_End", [ "left_shoulder" ] = "LeftShoulder", [ "left_arm" ] = "LeftArm",
		[ "left_forearm" ] = "LeftForeArm", [ "left_hand" ] = "LeftHand", [ "left_index" ] = "LeftHandIndex1",
		[ "left_thumb" ] = "LeftHandThumb1", [ "left_upleg" ] = "LeftUpLeg", [ "left_leg" ] = "LeftLeg", [ "left_foot" ] = "LeftFoot",
		[ "left_toe" ] = "LeftToeBase", [ "left_toe_end" ] = "LeftToe_End",
	};

	private static readonly Dictionary<string, string> Unreal = new()
	{
		[ "hips" ] = "pelvis", [ "spine" ] = "spine_01", [ "spine2" ] = "spine_02", [ "spine3" ] = "spine_03", [ "neck" ] = "neck_01",
		[ "head" ] = "head", [ "left_shoulder" ] = "clavicle_l", [ "left_arm" ] = "upperarm_l", [ "left_forearm" ] = "lowerarm_l",
		[ "left_hand" ] = "hand_l", [ "left_index" ] = "index_01_l", [ "left_thumb" ] = "thumb_01_l", [ "left_upleg" ] = "thigh_l",
		[ "left_leg" ] = "calf_l", [ "left_foot" ] = "foot_l", [ "left_toe" ] = "ball_l",
	};

	private static readonly Dictionary<string, string> Biped = new()
	{
		[ "root" ] = "Bip01", [ "hips" ] = "Bip01 Pelvis", [ "spine" ] = "Bip01 Spine", [ "spine2" ] = "Bip01 Spine1",
		[ "spine3" ] = "Bip01 Spine2", [ "neck" ] = "Bip01 Neck", [ "head" ] = "Bip01 Head", [ "left_shoulder" ] = "Bip01 L Clavicle",
		[ "left_arm" ] = "Bip01 L UpperArm", [ "left_forearm" ] = "Bip01 L Forearm", [ "left_hand" ] = "Bip01 L Hand",
		[ "left_index" ] = "Bip01 L Finger1", [ "left_thumb" ] = "Bip01 L Finger0", [ "left_upleg" ] = "Bip01 L Thigh",
		[ "left_leg" ] = "Bip01 L Calf", [ "left_foot" ] = "Bip01 L Foot", [ "left_toe" ] = "Bip01 L Toe0",
	};

	[Theory]
	[InlineData( "citizen" )]
	[InlineData( "mixamo" )]
	[InlineData( "unreal" )]
	[InlineData( "biped" )]
	[InlineData( "unnamed" )]
	public void FindsEveryRoleForCommonRigConventions( string convention )
	{
		var rig = Rig( convention, 0f, 0f, false );
		var bones = new HumanoidBones( rig.Names, rig.Parents, rig.Positions );
		Assert.True( bones.Complete );
		foreach ( var role in Roles )
			Assert.Equal( Array.IndexOf( rig.Roles, role ), bones[ role ] );
		Assert.True( Angle( Quaternion.Identity, bones.Facing ) < 0.01f );
	}

	[Theory]
	[InlineData( "citizen", "sbox" )]
	[InlineData( "mixamo", "mixamo" )]
	[InlineData( "unreal", "ue_mannequin" )]
	[InlineData( "unnamed", null )]
	public void RecognisesKnownRigFamilies( string convention, string preset )
	{
		var rig = Rig( convention, 0f, 0f, false );
		Assert.Equal( preset, new HumanoidBones( rig.Names, rig.Parents, rig.Positions ).Preset );
	}

	[Fact]
	public void MeasuresTheFacingOfARigBuiltSideways()
	{
		var rig = Rig( "mixamo", MathF.PI / 2f, 0f, false );
		var bones = new HumanoidBones( rig.Names, rig.Parents, rig.Positions );
		Assert.True( bones.Complete );
		Assert.Equal( Array.IndexOf( rig.Roles, "left_foot" ), bones[ "left_foot" ] );
		var forward = Vector3.Transform( Vector3.UnitX, bones.Facing );
		Assert.True( Vector3.Distance( forward, Vector3.UnitY ) < 0.01f );
	}

	[Fact]
	public void RetargetsOntoASidewaysAPoseRigWithRolledBones()
	{
		var source = Source();
		var rig = Rig( "mixamo", MathF.PI / 2f, MathF.PI / 4f, true );
		var bones = new HumanoidBones( rig.Names, rig.Parents, rig.Positions );
		var retargeter = new PoseRetargeter( source, rig.Names, rig.Parents, rig.Positions, rig.Rotations, bones.Map(), bones.Facing );
		var pose = new SkeletonPose( source.Count );
		Array.Copy( source.RestPositions, pose.Positions, source.Count );
		Array.Copy( source.RestRotations, pose.Rotations, source.Count );
		// Raise the left arm straight up and bend the right forearm forward.
		pose.Rotations[ source.IndexOf( "left_arm" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitX, MathF.PI / 2f );
		pose.Rotations[ source.IndexOf( "right_forearm" ) ] = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI / 2f );
		var positions = new Vector3[ rig.Names.Length ];
		var rotations = new Quaternion[ rig.Names.Length ];
		retargeter.Apply( pose, false, positions, rotations );

		Vector3 Segment( string from, string to ) =>
			Vector3.Normalize( positions[ Array.IndexOf( rig.Roles, to ) ] - positions[ Array.IndexOf( rig.Roles, from ) ] );
		// Outputs are in the clip frame whatever way the rig faces.
		Assert.True( Degrees( Segment( "left_arm", "left_forearm" ), Vector3.UnitZ ) < 2f );
		Assert.True( Degrees( Segment( "left_forearm", "left_hand" ), Vector3.UnitZ ) < 2f );
		// The hand carries on straight from the forearm although the rig rests in an A-pose.
		Assert.True( Degrees( Segment( "left_hand", "left_index" ), Vector3.UnitZ ) < 16f );
		Assert.True( Degrees( Segment( "right_forearm", "right_hand" ), Vector3.UnitX ) < 2f );
		Assert.True( Degrees( Segment( "left_leg", "left_foot" ), -Vector3.UnitZ ) < 2f );
		Assert.True( positions[ Array.IndexOf( rig.Roles, "left_foot" ) ].Y > 0.05f );
		// Converting back gives the rig's own model space.
		var model = retargeter.ToModel( positions[ Array.IndexOf( rig.Roles, "left_foot" ) ] );
		Assert.True( Vector3.Distance( model, rig.Positions[ Array.IndexOf( rig.Roles, "left_foot" ) ] ) < 0.02f );
	}

	[Fact]
	public void RestPoseRetargetsOntoTheTargetRest()
	{
		var source = Source();
		var rig = Rig( "unreal", 0f, MathF.PI / 4f, true );
		var bones = new HumanoidBones( rig.Names, rig.Parents, rig.Positions );
		var retargeter = new PoseRetargeter( source, rig.Names, rig.Parents, rig.Positions, rig.Rotations, bones.Map(), bones.Facing );
		var pose = new SkeletonPose( source.Count );
		Array.Copy( source.RestPositions, pose.Positions, source.Count );
		Array.Copy( source.RestRotations, pose.Rotations, source.Count );
		var positions = new Vector3[ rig.Names.Length ];
		var rotations = new Quaternion[ rig.Names.Length ];
		retargeter.Apply( pose, false, positions, rotations );
		// Legs and trunk keep the target's rest; arms are swung into the source's T-pose.
		foreach ( var role in new[] { "hips", "head", "left_foot", "right_toe" } )
		{
			var bone = Array.IndexOf( rig.Roles, role );
			Assert.True( Vector3.Distance( positions[ bone ], rig.Positions[ bone ] ) < 0.01f, role );
		}
		var arm = Vector3.Normalize( positions[ Array.IndexOf( rig.Roles, "left_forearm" ) ] - positions[ Array.IndexOf( rig.Roles, "left_arm" ) ] );
		Assert.True( Degrees( arm, Vector3.UnitY ) < 2f );
	}

	[Theory]
	[InlineData( "mixamorig:LeftUpLeg", "upleg" )]
	[InlineData( "J_Bip_L_UpperLeg", "upleg" )]
	[InlineData( "DEF-forearm.L", "forearm" )]
	[InlineData( "ValveBiped.Bip01_R_Calf", "leg" )]
	[InlineData( "CC_Base_L_Thigh", "upleg" )]
	[InlineData( "upperarm_twist_01_l", null )]
	[InlineData( "LeftHandIndex1", null )]
	public void ReadsRolesFromBoneNames( string name, string role ) =>
		Assert.Equal( role, HumanoidBones.Role( HumanoidBones.Words( name ) ) );

	// Clip skeleton with canonical names, local offsets and identity rest rotations.
	private static ClipSkeleton Source()
	{
		var body = Body.Where( bone => !bone.Role.Contains( "index" ) && !bone.Role.Contains( "thumb" ) && !bone.Role.EndsWith( "_end" ) ).ToArray();
		var names = body.Select( bone => bone.Role ).ToArray();
		var parents = body.Select( bone => Array.IndexOf( names, bone.Parent ) ).ToArray();
		var local = body.Select( ( bone, i ) => parents[ i ] < 0 ? bone.Position : bone.Position - body[ parents[ i ] ].Position ).ToArray();
		return new ClipSkeleton( names, parents, local, Enumerable.Repeat( Quaternion.Identity, names.Length ).ToArray() );
	}

	// The body under a naming convention, turned by yaw, with the arms dropped by armDrop and optional bone roll.
	private static (string[] Names, string[] Roles, int[] Parents, Vector3[] Positions, Quaternion[] Rotations) Rig(
		string convention, float yaw, float armDrop, bool roll )
	{
		var roles = Body.Select( bone => bone.Role ).ToArray();
		var names = roles.Select( ( role, i ) => Name( convention, role, i ) ).ToArray();
		var parents = Body.Select( bone => Array.IndexOf( roles, bone.Parent ) ).ToArray();
		var positions = Body.Select( bone => bone.Position ).ToArray();
		foreach ( var side in new[] { "left", "right" } )
		{
			// Swing the arm below each shoulder down about the forward axis.
			var shoulder = positions[ Array.IndexOf( roles, $"{side}_arm" ) ];
			var drop = Quaternion.CreateFromAxisAngle( Vector3.UnitX, side == "left" ? -armDrop : armDrop );
			for ( var i = 0; i < roles.Length; i++ )
			{
				if ( roles[ i ].StartsWith( side ) && roles[ i ] is var r && (r.Contains( "forearm" ) || r.Contains( "hand" ) || r.Contains( "index" ) || r.Contains( "thumb" )) )
					positions[ i ] = shoulder + Vector3.Transform( positions[ i ] - shoulder, drop );
			}
		}
		var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, yaw );
		positions = positions.Select( position => Vector3.Transform( position, turn ) ).ToArray();
		var rotations = roles.Select( ( _, i ) => roll
			? Quaternion.Normalize( turn * Quaternion.CreateFromYawPitchRoll( 0.3f * i, 0.7f - 0.1f * i, 0.2f * i ) )
			: turn ).ToArray();
		return (names, roles, parents, positions, rotations);
	}

	private static string Name( string convention, string role, int index )
	{
		if ( convention == "unnamed" )
			return $"bone_{index:00}";
		var table = convention switch { "citizen" => Citizen, "mixamo" => Mixamo, "unreal" => Unreal, _ => Biped };
		var left = role.StartsWith( "left_" ) ? role : role.StartsWith( "right_" ) ? "left_" + role[ 6.. ] : role;
		if ( !table.TryGetValue( left, out var name ) )
			name = convention == "mixamo" ? $"Extra{index}" : $"extra_{index}";
		else if ( role.StartsWith( "right_" ) )
		{
			name = convention switch
			{
				"citizen" => name[ ..^1 ] + "R",
				"unreal" => name[ ..^1 ] + "r",
				"biped" => name.Replace( " L ", " R " ),
				_ => name.Replace( "Left", "Right" )
			};
		}
		return convention == "mixamo" ? "mixamorig:" + name : name;
	}

	private static float Degrees( Vector3 a, Vector3 b ) =>
		MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( a ), Vector3.Normalize( b ) ), -1f, 1f ) ) * 180f / MathF.PI;

	private static float Angle( Quaternion a, Quaternion b ) =>
		2f * MathF.Acos( Math.Clamp( MathF.Abs( Quaternion.Dot( a, b ) ), 0f, 1f ) );
}
