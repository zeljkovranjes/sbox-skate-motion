using System.Linq;

namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The world turns and positions of a pose of the clip skeleton, measured parents first, and the body's own axes in it:
/// up from the hips to the head (or neck), and across from the right side to the left (the shoulders, else the arms,
/// else the legs). For layers that turn the upper body about the body's own axes whatever the clip does. Bones are found
/// by the humanoid role names the clips use; the buffers are reused, so measuring allocates nothing.
/// </summary>
internal sealed class UpperBodyFrame
{
	private readonly int _hips;
	private readonly int _top;
	private readonly int _left;
	private readonly int _right;

	internal UpperBodyFrame( ClipSkeleton skeleton )
	{
		Parents = skeleton.Parents;
		Order = ParentsFirst( skeleton.Parents );
		World = new Quaternion[ skeleton.Count ];
		Positions = new Vector3[ skeleton.Count ];
		_hips = skeleton.IndexOf( "hips" );
		var head = skeleton.IndexOf( "head" );
		_top = head >= 0 ? head : skeleton.IndexOf( "neck" );
		(_left, _right) = new[] { ("left_shoulder", "right_shoulder"), ("left_arm", "right_arm"), ("left_upleg", "right_upleg") }
			.Select( pair => (skeleton.IndexOf( pair.Item1 ), skeleton.IndexOf( pair.Item2 )) )
			.FirstOrDefault( pair => pair.Item1 >= 0 && pair.Item2 >= 0, (-1, -1) );
	}

	/// <summary>Each bone's parent (-1 for a root).</summary>
	internal int[] Parents { get; }

	/// <summary>Bone indices with every parent before its children.</summary>
	internal int[] Order { get; }

	/// <summary>World turns of the pose last measured.</summary>
	internal Quaternion[] World { get; }

	/// <summary>World positions of the pose last measured.</summary>
	internal Vector3[] Positions { get; }

	/// <summary>Whether the skeleton has the bones the body's axes are measured between.</summary>
	internal bool CanMeasure => _hips >= 0 && _top >= 0 && _left >= 0;

	/// <summary>
	/// Measures <paramref name="pose"/>'s world turns and positions and the body's axes in it; false when the body has no
	/// axes to measure (a pose with its head on its hips, or its sides on one line up the body).
	/// </summary>
	internal bool Measure( SkeletonPose pose, out Vector3 up, out Vector3 across )
	{
		up = across = Vector3.Zero;
		if ( !CanMeasure )
			return false;
		foreach ( var bone in Order )
		{
			var parent = Parents[ bone ];
			var rotation = Quaternion.Normalize( pose.Rotations[ bone ] );
			if ( parent < 0 )
			{
				World[ bone ] = rotation;
				Positions[ bone ] = pose.Positions[ bone ];
				continue;
			}
			World[ bone ] = Quaternion.Normalize( World[ parent ] * rotation );
			Positions[ bone ] = Positions[ parent ] + Vector3.Transform( pose.Positions[ bone ], World[ parent ] );
		}
		up = Positions[ _top ] - Positions[ _hips ];
		if ( up.LengthSquared() < 1e-10f )
			return false;
		up = Vector3.Normalize( up );
		across = Positions[ _left ] - Positions[ _right ];
		across -= up * Vector3.Dot( across, up );
		if ( across.LengthSquared() < 1e-10f )
			return false;
		across = Vector3.Normalize( across );
		return true;
	}

	/// <summary>The local rotation that turns a bone, under a parent now at <paramref name="parentWorld"/>, by
	/// <paramref name="angle"/> radians about a world axis.</summary>
	internal static Quaternion Turned( Quaternion parentWorld, Quaternion local, Vector3 axis, float angle )
	{
		var turn = Quaternion.CreateFromAxisAngle( axis, angle );
		return Quaternion.Normalize( Quaternion.Conjugate( parentWorld ) * turn * parentWorld * local );
	}

	// Bone indices ordered so every parent comes before its children.
	private static int[] ParentsFirst( int[] parents )
	{
		int Depth( int bone )
		{
			var depth = 0;
			for ( var at = parents[ bone ]; at >= 0 && depth <= parents.Length; at = parents[ at ] )
				depth++;
			return depth;
		}
		return Enumerable.Range( 0, parents.Length ).OrderBy( Depth ).ToArray();
	}
}
