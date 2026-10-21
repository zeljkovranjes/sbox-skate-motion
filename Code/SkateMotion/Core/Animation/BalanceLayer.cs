using System.Linq;

namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Lays a balancing sway over a pose of the clip skeleton: the spine leans a little forward and back and from side to
/// side from the hips up, the neck takes most of it back out so the head stays steady, and the upper arms swing the other
/// way, as a rider holding a slide keeps its weight over the rail. Bones are found by the humanoid role names the clips
/// use (spine to spine3, neck, neck1, each side's arm); any that are missing are left out. Turns are taken about the
/// body's own axes in the pose given, and nothing at or below the hips (the legs, the feet, the board) moves.
/// </summary>
internal sealed class BalanceLayer
{
	/// <summary>Degrees the spine leans forward or back, and to either side, at the sway's fullest.</summary>
	internal const float PitchLean = 2f;

	internal const float RollLean = 1.5f;

	/// <summary>Share of the spine's lean the neck takes back out, so the head holds steady over the rail.</summary>
	internal const float NeckSteady = 0.7f;

	/// <summary>Degrees the upper arms swing against the lean at the sway's fullest.</summary>
	internal const float ArmSwing = 2f;

	private enum Part
	{
		None,
		Spine,
		Neck,
		Arm
	}

	private readonly UpperBodyFrame _frame;
	private readonly Part[] _parts;
	private readonly float[] _shares;
	private readonly Quaternion[] _swayed;

	internal BalanceLayer( ClipSkeleton skeleton )
	{
		_frame = new UpperBodyFrame( skeleton );
		_parts = new Part[ skeleton.Count ];
		_shares = new float[ skeleton.Count ];
		_swayed = new Quaternion[ skeleton.Count ];
		Spread( skeleton, HumanoidBones.Spine, Part.Spine );
		Spread( skeleton, new[] { "neck", "neck1" }, Part.Neck );
		foreach ( var arm in new[] { "left_arm", "right_arm" }.Select( skeleton.IndexOf ).Where( bone => bone >= 0 ) )
			_parts[ arm ] = Part.Arm;
	}

	/// <summary>
	/// Sways <paramref name="pose"/> (local transforms, changed in place) by <paramref name="pitch"/> forward (negative
	/// back) and <paramref name="roll"/> to the body's left (negative right), each -1 to 1 at the sway's fullest.
	/// </summary>
	internal void Apply( SkeletonPose pose, float pitch, float roll )
	{
		if ( !float.IsFinite( pitch + roll ) || MathF.Abs( pitch ) + MathF.Abs( roll ) < 1e-4f
			|| !_frame.Measure( pose, out var up, out var across ) )
			return;
		// About the line across the body a positive turn tips the top forward; about the forward line it tips it right.
		var forward = System.Numerics.Vector3.Cross( across, up );
		var tip = Math.Clamp( pitch, -1f, 1f ) * PitchLean * MathF.PI / 180f;
		var lean = Math.Clamp( roll, -1f, 1f ) * RollLean * MathF.PI / 180f;
		var swing = ArmSwing / MathF.Max( PitchLean, RollLean );
		foreach ( var bone in _frame.Order )
		{
			var parent = _frame.Parents[ bone ];
			var parentWorld = parent < 0 ? Quaternion.Identity : _swayed[ parent ];
			var local = pose.Rotations[ bone ];
			switch ( _parts[ bone ] )
			{
				case Part.Spine:
					local = UpperBodyFrame.Turned( parentWorld, local, across, tip * _shares[ bone ] );
					local = UpperBodyFrame.Turned( parentWorld, local, forward, -lean * _shares[ bone ] );
					break;
				case Part.Neck:
					local = UpperBodyFrame.Turned( parentWorld, local, across, -NeckSteady * tip * _shares[ bone ] );
					local = UpperBodyFrame.Turned( parentWorld, local, forward, NeckSteady * lean * _shares[ bone ] );
					break;
				case Part.Arm:
				{
					// The upper arm keeps its turn in the world and swings against the lean, as a counterweight: back as the
					// body tips forward (a hanging arm turned about the line across the body the way the top tips forward
					// swings back), and to the right as it leans left.
					var hanging = Quaternion.Normalize( Quaternion.Conjugate( parentWorld ) * _frame.World[ bone ] );
					local = UpperBodyFrame.Turned( parentWorld, hanging, across, swing * tip );
					local = UpperBodyFrame.Turned( parentWorld, local, forward, -swing * lean );
					break;
				}
			}
			pose.Rotations[ bone ] = local;
			_swayed[ bone ] = Quaternion.Normalize( parentWorld * local );
		}
	}

	// Marks the named bones that exist as one part, sharing its turn evenly.
	private void Spread( ClipSkeleton skeleton, string[] names, Part part )
	{
		var bones = names.Select( skeleton.IndexOf ).Where( bone => bone >= 0 ).ToArray();
		foreach ( var bone in bones )
		{
			_parts[ bone ] = part;
			_shares[ bone ] = 1f / bones.Length;
		}
	}
}
