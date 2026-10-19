using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

/// <summary>
/// Keeps a held board out of the rider's body. The body is a set of capsules along the legs, torso and free arm,
/// with each radius measured from the rider's own mesh at rest, so any humanoid model fits. A board that would
/// pass into a capsule is moved out from the body until it clears; the holding arm reaches out with it.
/// </summary>
internal sealed class BodyClearance
{
	private readonly (int From, int To, float Radius)[] _capsules;

	// Inches kept between skin and board.
	private const float Margin = 0.3f;

	// Furthest the board is moved out, in inches; the holding arm reaches it there.
	private const float PushLimit = 6f;

	private const int Samples = 9;

	/// <param name="model">The rider's model; its mesh is traced at rest to size each capsule.</param>
	/// <param name="rig">The rider's humanoid bones.</param>
	/// <param name="holdingHand">Bone of the hand holding the board; its arm may touch the board.</param>
	internal BodyClearance( Model model, HumanoidBones rig, int holdingHand )
	{
		var bones = model.Bones.AllBones.ToDictionary( bone => bone.Index, bone => bone.LocalTransform.Position );
		var segments = new List<(string From, string To)>
		{
			("hips", "neck"),
			("left_upleg", "left_leg"), ("left_leg", "left_foot"), ("left_foot", "left_toe"),
			("right_upleg", "right_leg"), ("right_leg", "right_foot"), ("right_foot", "right_toe")
		};
		foreach ( var side in new[] { "left", "right" } )
		{
			if ( rig[ $"{side}_hand" ] == holdingHand )
				continue;
			segments.Add( ($"{side}_arm", $"{side}_forearm") );
			segments.Add( ($"{side}_forearm", $"{side}_hand") );
		}
		_capsules = segments
			.Select( segment => (From: rig[ segment.From ], To: rig[ segment.To ] >= 0 || segment.To != "neck" ? rig[ segment.To ] : rig[ "head" ]) )
			.Where( segment => segment.From >= 0 && segment.To >= 0 && segment.From != segment.To )
			.Select( segment => (segment.From, segment.To, Radius( model, bones[ segment.From ], bones[ segment.To ] )) )
			.ToArray();
	}

	/// <summary>Measured capsules as (from bone, to bone, radius in inches).</summary>
	internal IReadOnlyList<(int From, int To, float Radius)> Capsules => _capsules;

	/// <summary>
	/// How far <paramref name="board"/> must move along <paramref name="away"/> for its box to clear the body, in
	/// inches (zero when it already clears).
	/// </summary>
	/// <param name="board">Where the board would be shown.</param>
	/// <param name="box">The whole board (deck, trucks, wheels) in its own frame.</param>
	/// <param name="away">Unit direction from the body towards the board.</param>
	/// <param name="bone">World position of a rider bone.</param>
	internal float Push( Transform board, BBox box, Vector3 away, System.Func<int, Vector3> bone )
	{
		var shift = 0f;
		for ( var iteration = 0; iteration < 8 && shift < PushLimit; iteration++ )
		{
			var (depth, _, normal) = Deepest( board.WithPosition( board.Position + away * shift ), box, bone );
			if ( depth <= 0.01f )
				break;
			// Moving along away opens the gap by the share of away that points from the body to the board.
			shift = MathF.Min( shift + depth / MathF.Max( -Vector3.Dot( normal, away ), 0.3f ), PushLimit );
		}
		return shift;
	}

	// The capsule point reaching furthest into the board: depth (inches), the touching point on the board and the
	// direction from the board to the body there, in world space.
	private (float Depth, Vector3 Contact, Vector3 Normal) Deepest( Transform board, BBox box, System.Func<int, Vector3> bone )
	{
		var best = (Depth: 0f, Contact: Vector3.Zero, Normal: Vector3.Zero);
		foreach ( var (from, to, radius) in _capsules )
		{
			var start = bone( from );
			var end = bone( to );
			for ( var i = 0; i < Samples; i++ )
			{
				var point = board.PointToLocal( Vector3.Lerp( start, end, i / (Samples - 1f) ) );
				var closest = point.Clamp( box.Mins, box.Maxs );
				Vector3 normal;
				float distance;
				if ( closest != point )
				{
					normal = (point - closest).Normal;
					distance = (point - closest).Length;
				}
				else
				{
					// Inside the box: leave through the nearest face.
					var toMin = point - box.Mins;
					var toMax = box.Maxs - point;
					var faces = new[] { (toMin.x, -Vector3.Forward), (toMax.x, Vector3.Forward), (toMin.y, -Vector3.Left),
						(toMax.y, Vector3.Left), (toMin.z, -Vector3.Up), (toMax.z, Vector3.Up) };
					var face = faces.MinBy( f => f.Item1 );
					normal = face.Item2;
					distance = -face.Item1;
					closest = point - normal * distance;
				}
				var depth = radius + Margin - distance;
				if ( depth > best.Depth )
					best = (depth, board.PointToWorld( closest ), board.Rotation * normal);
			}
		}
		return best;
	}

	/// <summary>
	/// A hand measured from the rider's mesh at rest: the direction from the wrist to the fingertips in the hand
	/// bone's own frame, the distance to the fingertips and the hand's typical thickness (inches); null when the
	/// hand has no forearm or its fingertips cannot be traced.
	/// </summary>
	internal static (Vector3 Axis, float Length, float Radius)? Hand( Model model, HumanoidBones rig, string side )
	{
		var hand = rig[ $"{side}_hand" ];
		var forearm = rig[ $"{side}_forearm" ];
		if ( hand < 0 || forearm < 0 )
			return null;
		var bones = model.Bones.AllBones;
		var bone = bones.First( b => b.Index == hand );
		var wrist = bone.LocalTransform.Position;
		var elbow = bones.First( b => b.Index == forearm ).LocalTransform.Position;
		// The fingers hang from the hand bone and their middle shows where the hand points; without finger bones the
		// hand carries on along the forearm.
		var fingers = new List<Vector3>();
		Descendants( bone, fingers );
		var toward = fingers.Count > 0 ? fingers.Aggregate( Vector3.Zero, ( sum, point ) => sum + point ) / fingers.Count - wrist
			: wrist - elbow;
		if ( toward.Length < 0.01f )
			toward = wrist - elbow;
		var axis = toward.Normal;
		// The fingertips are where the mesh begins, coming back to the wrist from a forearm's length beyond it.
		var reach = MathF.Max( (wrist - elbow).Length, 1f );
		var tip = model.Trace.Ray( wrist + axis * reach, wrist ).Run();
		var length = tip.Hit ? Vector3.Dot( tip.HitPosition - wrist, axis ) : 0f;
		if ( length < reach * 0.1f )
			return null;
		return (bone.LocalTransform.Rotation.Inverse * axis, length, Radius( model, wrist, wrist + axis * length ));
	}

	private static void Descendants( BoneCollection.Bone bone, List<Vector3> positions )
	{
		foreach ( var child in bone.Children )
		{
			positions.Add( child.LocalTransform.Position );
			Descendants( child, positions );
		}
	}

	// Typical distance from the bone line to the skin, from rays fired at the line from around it at rest. Rays that
	// meet another body part first read long, so the median is used; a model without a traceable mesh falls back to
	// a share of the segment's length.
	internal static float Radius( Model model, Vector3 from, Vector3 to )
	{
		var length = (to - from).Length;
		var fallback = length * 0.18f;
		var axis = (to - from).Normal;
		var side = MathF.Abs( Vector3.Dot( axis, Vector3.Up ) ) < 0.9f ? Vector3.Cross( axis, Vector3.Up ).Normal : Vector3.Cross( axis, Vector3.Forward ).Normal;
		var other = Vector3.Cross( axis, side );
		var reach = MathF.Max( length * 0.6f, 6f );
		var hits = new List<float>();
		foreach ( var along in new[] { 0.3f, 0.5f, 0.7f } )
		{
			var centre = Vector3.Lerp( from, to, along );
			for ( var i = 0; i < 8; i++ )
			{
				var angle = i * MathF.PI / 4f;
				var direction = side * MathF.Cos( angle ) + other * MathF.Sin( angle );
				var trace = model.Trace.Ray( centre + direction * reach, centre ).Run();
				if ( trace.Hit )
					hits.Add( (trace.HitPosition - centre).Length );
			}
		}
		if ( hits.Count < 6 )
			return fallback;
		hits.Sort();
		return Math.Clamp( hits[ hits.Count / 2 ], length * 0.05f, length * 0.45f );
	}
}
