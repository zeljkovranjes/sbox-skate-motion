using System.Linq;

namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Lays a breath over a pose of the clip skeleton. Breathing in, the spine straightens a little from the hips up so the
/// chest lifts, the collarbones raise the shoulders, the upper arms keep hanging the way the clip holds them, and the
/// head tips up a touch; breathing out undoes it. Bones are found by the humanoid role names the clips use (spine to
/// spine3, neck, neck1, head, and each side's shoulder and arm); any that are missing are left out. Every turn is taken
/// about the body's own axes in the pose given (across the shoulders, up from the hips), so the breath follows whatever
/// the clip does, and nothing at or below the hips (the legs, the feet, the board) moves.
/// </summary>
internal sealed class BreathingLayer
{
	/// <summary>Degrees the spine leans back from the hips up when fully breathed in at resting depth.</summary>
	internal const float SpineLean = 2.5f;

	/// <summary>Share of the spine's lean back the neck takes out again, so the head stays nearly level.</summary>
	internal const float NeckReturn = 0.85f;

	/// <summary>Degrees the head tips up on its own when fully breathed in at resting depth.</summary>
	internal const float HeadNod = 0.3f;

	/// <summary>Degrees each collarbone lifts its shoulder when fully breathed in at resting depth.</summary>
	internal const float ShoulderLift = 3f;

	private enum Part
	{
		None,
		Spine,
		Neck,
		Head,
		Shoulder,
		Arm
	}

	private readonly UpperBodyFrame _frame;
	private readonly Part[] _parts;

	// Each spine and neck bone's share of its part's turn, and each shoulder's arm (the bone it lifts toward).
	private readonly float[] _shares;
	private readonly int[] _shoulderArm;

	// The rotations as breathed, reused every frame.
	private readonly Quaternion[] _breathed;

	internal BreathingLayer( ClipSkeleton skeleton )
	{
		var count = skeleton.Count;
		_frame = new UpperBodyFrame( skeleton );
		_parts = new Part[ count ];
		_shares = new float[ count ];
		_shoulderArm = new int[ count ];
		Array.Fill( _shoulderArm, -1 );
		_breathed = new Quaternion[ count ];
		Spread( skeleton, HumanoidBones.Spine, Part.Spine );
		Spread( skeleton, new[] { "neck", "neck1" }, Part.Neck );
		var head = skeleton.IndexOf( "head" );
		if ( head >= 0 )
			_parts[ head ] = Part.Head;
		foreach ( var side in new[] { "left", "right" } )
		{
			var shoulder = skeleton.IndexOf( side + "_shoulder" );
			var arm = skeleton.IndexOf( side + "_arm" );
			if ( shoulder >= 0 && arm >= 0 )
			{
				_parts[ shoulder ] = Part.Shoulder;
				_shoulderArm[ shoulder ] = arm;
			}
			if ( arm >= 0 )
				_parts[ arm ] = Part.Arm;
		}
	}

	/// <summary>
	/// Breathes <paramref name="pose"/> (local transforms, changed in place) in by <paramref name="inhale"/>: 0 leaves it
	/// as it is, 1 is a full resting breath, more is deeper.
	/// </summary>
	internal void Apply( SkeletonPose pose, float inhale )
	{
		if ( !(inhale > 0f) || !float.IsFinite( inhale ) || !_frame.Measure( pose, out var up, out var across ) )
			return;
		var radians = inhale * MathF.PI / 180f;
		foreach ( var bone in _frame.Order )
		{
			var parent = _frame.Parents[ bone ];
			var parentWorld = parent < 0 ? Quaternion.Identity : _breathed[ parent ];
			var local = pose.Rotations[ bone ];
			switch ( _parts[ bone ] )
			{
				case Part.Spine:
					local = UpperBodyFrame.Turned( parentWorld, local, across, -SpineLean * _shares[ bone ] * radians );
					break;
				case Part.Neck:
					local = UpperBodyFrame.Turned( parentWorld, local, across, NeckReturn * SpineLean * _shares[ bone ] * radians );
					break;
				case Part.Head:
					local = UpperBodyFrame.Turned( parentWorld, local, across, -HeadNod * radians );
					break;
				case Part.Shoulder:
				{
					// Turning the collarbone from its arm toward the head lifts the shoulder, whichever side it is on.
					var axis = Vector3.Cross( _frame.Positions[ _shoulderArm[ bone ] ] - _frame.Positions[ bone ], up );
					if ( axis.LengthSquared() > 1e-10f )
						local = UpperBodyFrame.Turned( parentWorld, local, Vector3.Normalize( axis ), ShoulderLift * radians );
					break;
				}
				case Part.Arm:
					// The upper arm keeps hanging as the clip held it: only its shoulder moves.
					local = Quaternion.Normalize( Quaternion.Conjugate( parentWorld ) * _frame.World[ bone ] );
					break;
			}
			pose.Rotations[ bone ] = local;
			_breathed[ bone ] = Quaternion.Normalize( parentWorld * local );
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
