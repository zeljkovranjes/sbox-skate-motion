using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Transfers a clip pose onto another skeleton. Each mapped target bone takes the source bone's rotation change
/// from rest, measured in the source root's frame, applied to its rest pose corrected toward the source's (so
/// A-pose and T-pose rigs agree); limbs are then swung to point where the source limbs point. Target bone
/// lengths are kept and the mapped root bone's position is scaled by the ratio of hip heights above the soles.
/// Unmapped bones follow their parents. Output is in the clip frame (+X forward, +Y left, +Z up, ground at
/// zero) in target units; <see cref="Facing"/> and <see cref="Ground"/> turn it back into the rig's model space.
/// </summary>
internal sealed class PoseRetargeter
{
	private readonly ClipSkeleton _source;
	private readonly int[] _targetParents;
	private readonly Vector3[] _bindPositions;
	private readonly Quaternion[] _bindRotations;
	private readonly Quaternion[] _correction;
	private readonly int[] _map;
	private readonly int[] _aim;
	private readonly int[] _order;
	private readonly int[] _mirror;
	private readonly int[] _effectors;
	private readonly int _anchor;
	private readonly float _scale;
	private readonly Vector3[] _restPositions;
	private readonly Quaternion[] _restRotations;
	private readonly Vector3[] _sourcePositions;
	private readonly Quaternion[] _sourceRotations;

	/// <param name="source">Clip skeleton.</param>
	/// <param name="targetNames">Target bone names.</param>
	/// <param name="targetParents">Target parent indices, -1 for roots.</param>
	/// <param name="bindPositions">Target bind positions in model space.</param>
	/// <param name="bindRotations">Target bind rotations in model space.</param>
	/// <param name="map">Target bone name to source bone name.</param>
	/// <param name="facing">Rotation from the clip frame to the target's model space.</param>
	/// <param name="ground">Height of the target's soles in the clip frame (model space rotated by the inverse facing).</param>
	internal PoseRetargeter( ClipSkeleton source, string[] targetNames, int[] targetParents,
		Vector3[] bindPositions, Quaternion[] bindRotations, IReadOnlyDictionary<string, string> map,
		Quaternion facing = default, float ground = 0f )
	{
		_source = source;
		_targetParents = targetParents;
		Facing = facing == default ? Quaternion.Identity : Quaternion.Normalize( facing );
		Ground = ground;
		var inverse = Quaternion.Conjugate( Facing );
		_bindPositions = bindPositions.Select( position => Vector3.Transform( position, inverse ) - new Vector3( 0f, 0f, ground ) ).ToArray();
		_bindRotations = bindRotations.Select( rotation => Quaternion.Normalize( inverse * rotation ) ).ToArray();
		bindPositions = _bindPositions;
		_correction = Enumerable.Repeat( Quaternion.Identity, targetNames.Length ).ToArray();
		_map = targetNames.Select( name => map.TryGetValue( name, out var bone ) ? source.IndexOf( bone ) : -1 ).ToArray();
		_order = Enumerable.Range( 0, targetNames.Length ).OrderBy( bone => Depth( targetParents, bone ) ).ToArray();
		_aim = Enumerable.Range( 0, targetNames.Length ).Select( AimChild ).ToArray();
		_mirror = Enumerable.Range( 0, source.Count ).Select( bone => source.IndexOf( Swap( source.Names[ bone ] ) ) )
			.Select( ( other, bone ) => other < 0 ? bone : other ).ToArray();
		_effectors = source.Names.Select( name => source.IndexOf( "target_" +
			(name.EndsWith( "_toe", StringComparison.OrdinalIgnoreCase ) ? name[ ..^4 ] + "_foot" : name) ) )
			.Select( ( helper, bone ) => helper < 0 ? bone : helper ).ToArray();
		_restPositions = new Vector3[ source.Count ];
		_restRotations = new Quaternion[ source.Count ];
		_sourcePositions = new Vector3[ source.Count ];
		_sourceRotations = new Quaternion[ source.Count ];
		Solve( source.RestPositions, source.RestRotations, _restPositions, _restRotations );
		_anchor = _order.FirstOrDefault( bone => _map[ bone ] >= 0 && !Mapped( targetParents[ bone ] ), -1 );
		if ( _anchor < 0 )
			throw new ArgumentException( "No target bone maps to a source bone." );
		var sourceHeight = _restPositions[ _map[ _anchor ] ].Z;
		_scale = sourceHeight > 0.0001f && bindPositions[ _anchor ].Z > 0.0001f ? bindPositions[ _anchor ].Z / sourceHeight : 1f;
		// Rest correction: retarget the source rest pose once; whatever swing the aim step adds there is the
		// difference between the two rest poses. Hands have no limb to aim and inherit their forearm's, so a
		// hand stays straight on an arm rotated out of an A-pose; the head and other end bones keep their own
		// rest orientation, since a neck's rest lean says nothing about where the face points.
		var positions = new Vector3[ targetNames.Length ];
		var rotations = new Quaternion[ targetNames.Length ];
		var rest = new SkeletonPose( source.Count );
		Array.Copy( source.RestPositions, rest.Positions, source.Count );
		Array.Copy( source.RestRotations, rest.Rotations, source.Count );
		Apply( rest, false, positions, rotations );
		foreach ( var bone in _order )
		{
			if ( _map[ bone ] < 0 )
				continue;
			var parent = _targetParents[ bone ];
			if ( _aim[ bone ] >= 0 || parent < 0 )
				_correction[ bone ] = Quaternion.Normalize( rotations[ bone ] * Quaternion.Conjugate( _bindRotations[ bone ] ) );
			else if ( source.Names[ _map[ bone ] ].EndsWith( "hand", StringComparison.OrdinalIgnoreCase ) )
				_correction[ bone ] = Quaternion.Normalize( rotations[ parent ] * Quaternion.Conjugate( _bindRotations[ parent ] ) );
		}
	}

	/// <summary>Rotation from the clip frame to the target's model space.</summary>
	internal Quaternion Facing { get; }

	/// <summary>Height of the target's soles in the clip frame, before the bind was moved onto zero.</summary>
	internal float Ground { get; }

	/// <summary>Converts a clip-frame position from <see cref="Apply"/> into the target's model space.</summary>
	internal Vector3 ToModel( Vector3 position ) => Vector3.Transform( position + new Vector3( 0f, 0f, Ground ), Facing );

	/// <summary>Converts a clip-frame rotation from <see cref="Apply"/> into the target's model space.</summary>
	internal Quaternion ToModel( Quaternion rotation ) => Quaternion.Normalize( Facing * rotation );

	/// <summary>Target bind position of a bone in the clip frame.</summary>
	internal Vector3 BindPosition( int bone ) => _bindPositions[ bone ];

	/// <summary>Bind rotation of a target bone in the retargeting frame.</summary>
	internal Quaternion BindRotation( int bone ) => _bindRotations[ bone ];

	/// <summary>Target units per source metre, from the anchor bones' rest heights.</summary>
	internal float Scale => _scale;

	/// <summary>
	/// Position of a source bone in the source root frame for the last applied pose, in target units
	/// (mirrored when the pose was), or zero when the bone does not exist.
	/// </summary>
	internal Vector3 SourceOffset( int sourceBone, bool mirror )
	{
		if ( sourceBone < 0 )
			return Vector3.Zero;
		var position = _sourcePositions[ mirror ? _mirror[ sourceBone ] : sourceBone ];
		return (mirror ? new Vector3( position.X, -position.Y, position.Z ) : position) * _scale;
	}

	/// <summary>Source bone driving a target bone (the other side's when mirrored), or -1.</summary>
	internal int SourceBone( int targetBone, bool mirror )
	{
		var source = _map[ targetBone ];
		return source < 0 || !mirror ? source : _mirror[ source ];
	}

	/// <summary>Authored helper for a mapped effector, or its visible bone when no helper exists; swaps sides when mirrored.</summary>
	internal int SourceEffector( int targetBone, bool mirror )
	{
		var source = SourceBone( targetBone, mirror );
		return source < 0 ? -1 : _effectors[ source ];
	}

	/// <summary>Source bone position in the source root frame for the last applied pose, in source metres, unmirrored.</summary>
	internal Vector3 SourcePosition( int sourceBone ) => _sourcePositions[ sourceBone ];

	/// <summary>Source bone rotation in the source root frame for the last applied pose, unmirrored.</summary>
	internal Quaternion SourceRotation( int sourceBone ) => _sourceRotations[ sourceBone ];

	/// <summary>Source bone rotation in the source root frame, reflected across the side axis when mirrored.</summary>
	internal Quaternion SourceRotation( int sourceBone, bool mirror )
	{
		var source = mirror ? _mirror[ sourceBone ] : sourceBone;
		return mirror ? Reflect( _sourceRotations[ source ] ) : _sourceRotations[ source ];
	}

	/// <summary>
	/// Where a source bone sits relative to another (a board) in that bone's own frame, in target units, for the
	/// last applied pose; mirrored poses reflect it across the other bone's side axis.
	/// </summary>
	internal Vector3 SourceRelative( int sourceBone, int frameBone, bool mirror )
	{
		var offset = Vector3.Transform( _sourcePositions[ sourceBone ] - _sourcePositions[ frameBone ],
			Quaternion.Conjugate( _sourceRotations[ frameBone ] ) ) * _scale;
		return mirror ? new Vector3( offset.X, -offset.Y, offset.Z ) : offset;
	}

	/// <summary>Source ground height in the source root frame for the last applied pose (the root sits above it).</summary>
	internal float SourceGround { get; private set; }

	/// <summary>
	/// Height of the source bone driving <paramref name="targetBone"/> above the source ground, minus its rest
	/// height, in target units: zero means the source foot is planted. Null when the bone is unmapped.
	/// </summary>
	internal float? SourceClearance( int targetBone, bool mirror )
	{
		var source = _map[ targetBone ];
		if ( source < 0 )
			return null;
		if ( mirror )
			source = _mirror[ source ];
		return (_sourcePositions[ source ].Z - SourceGround - _restPositions[ source ].Z) * _scale;
	}

	/// <summary>Writes target model-space transforms for <paramref name="pose"/>.</summary>
	internal void Apply( SkeletonPose pose, bool mirror, Span<Vector3> positions, Span<Quaternion> rotations )
	{
		Solve( pose.Positions, pose.Rotations, _sourcePositions, _sourceRotations );
		// Root bones carry the clip's travel frame above the ground; their height is where the ground lies below.
		SourceGround = -pose.Positions[ 0 ].Z;
		foreach ( var bone in _order )
		{
			var parent = _targetParents[ bone ];
			var source = _map[ bone ];
			if ( source >= 0 )
			{
				var from = mirror ? _mirror[ source ] : source;
				var change = _sourceRotations[ from ] * Quaternion.Conjugate( _restRotations[ from ] );
				if ( mirror )
					change = Reflect( change );
				rotations[ bone ] = Quaternion.Normalize( change * _correction[ bone ] * _bindRotations[ bone ] );
			}
			else if ( parent >= 0 )
			{
				rotations[ bone ] = Quaternion.Normalize( ParentChange( parent, rotations ) * _bindRotations[ bone ] );
			}
			else
			{
				rotations[ bone ] = _bindRotations[ bone ];
			}

			if ( bone == _anchor )
			{
				var position = _sourcePositions[ mirror ? _mirror[ source ] : source ];
				positions[ bone ] = (mirror ? new Vector3( position.X, -position.Y, position.Z ) : position) * _scale;
			}
			else if ( parent >= 0 )
			{
				positions[ bone ] = positions[ parent ]
					+ Vector3.Transform( _bindPositions[ bone ] - _bindPositions[ parent ], ParentChange( parent, rotations ) );
			}
			else
			{
				positions[ bone ] = _bindPositions[ bone ];
			}
			Aim( bone, mirror, positions, rotations );
		}
	}

	// Rest poses differ between skeletons, so a rotation change alone leaves a constant angle error on every
	// limb. Swing each mapped bone so its segment points where the source segment points.
	private void Aim( int bone, bool mirror, Span<Vector3> positions, Span<Quaternion> rotations )
	{
		var child = _aim[ bone ];
		if ( child < 0 )
			return;
		var from = _map[ bone ];
		var to = _map[ child ];
		if ( mirror )
		{
			from = _mirror[ from ];
			to = _mirror[ to ];
		}
		var wanted = _sourcePositions[ to ] - _sourcePositions[ from ];
		if ( mirror )
			wanted.Y = -wanted.Y;
		var current = Vector3.Transform( _bindPositions[ child ] - _bindPositions[ bone ],
			rotations[ bone ] * Quaternion.Conjugate( _bindRotations[ bone ] ) );
		if ( wanted.LengthSquared() < 1e-10f || current.LengthSquared() < 1e-10f )
			return;
		rotations[ bone ] = Quaternion.Normalize( Between( current, wanted ) * rotations[ bone ] );
	}

	// The mapped child that continues this bone's chain: the one whose source bone comes first.
	private int AimChild( int bone )
	{
		if ( _map[ bone ] < 0 )
			return -1;
		var best = -1;
		for ( var child = 0; child < _targetParents.Length; child++ )
		{
			if ( _targetParents[ child ] != bone || _map[ child ] < 0 || !SourceDescends( _map[ child ], _map[ bone ] ) )
				continue;
			if ( best < 0 || _map[ child ] < _map[ best ] )
				best = child;
		}
		return best;
	}

	private bool SourceDescends( int bone, int ancestor )
	{
		for ( bone = _source.Parents[ bone ]; bone >= 0; bone = _source.Parents[ bone ] )
		{
			if ( bone == ancestor )
				return true;
		}
		return false;
	}

	private static Quaternion Between( Vector3 from, Vector3 to )
	{
		from = Vector3.Normalize( from );
		to = Vector3.Normalize( to );
		var dot = Vector3.Dot( from, to );
		if ( dot > 0.999999f )
			return Quaternion.Identity;
		var axis = Vector3.Cross( from, to );
		if ( axis.LengthSquared() < 1e-12f )
			axis = MathF.Abs( from.X ) < 0.9f ? Vector3.Cross( from, Vector3.UnitX ) : Vector3.Cross( from, Vector3.UnitY );
		return Quaternion.CreateFromAxisAngle( Vector3.Normalize( axis ), MathF.Acos( Math.Clamp( dot, -1f, 1f ) ) );
	}

	private Quaternion ParentChange( int parent, Span<Quaternion> rotations ) =>
		rotations[ parent ] * Quaternion.Conjugate( _bindRotations[ parent ] );

	// Forward kinematics in the source root's frame: root bones are placed at the origin unrotated.
	private void Solve( Vector3[] localPositions, Quaternion[] localRotations, Vector3[] positions, Quaternion[] rotations )
	{
		for ( var bone = 0; bone < _source.Count; bone++ )
		{
			var parent = _source.Parents[ bone ];
			if ( parent < 0 )
			{
				positions[ bone ] = Vector3.Zero;
				rotations[ bone ] = Quaternion.Identity;
				continue;
			}
			positions[ bone ] = positions[ parent ] + Vector3.Transform( localPositions[ bone ], rotations[ parent ] );
			rotations[ bone ] = Quaternion.Normalize( rotations[ parent ] * localRotations[ bone ] );
		}
	}

	private bool Mapped( int bone )
	{
		for ( ; bone >= 0; bone = _targetParents[ bone ] )
		{
			if ( _map[ bone ] >= 0 )
				return true;
		}
		return false;
	}

	// Reflection through the X-Z plane (left and right swap).
	private static Quaternion Reflect( Quaternion q ) => new( -q.X, q.Y, -q.Z, q.W );

	private static string Swap( string name )
	{
		if ( name.Contains( "left", StringComparison.OrdinalIgnoreCase ) )
			return ReplaceIgnoringCase( name, "left", "right" );
		if ( name.Contains( "right", StringComparison.OrdinalIgnoreCase ) )
			return ReplaceIgnoringCase( name, "right", "left" );
		return name;
	}

	private static string ReplaceIgnoringCase( string text, string from, string to )
	{
		var at = text.IndexOf( from, StringComparison.OrdinalIgnoreCase );
		return text[ ..at ] + to + text[ (at + from.Length).. ];
	}

	private static int Depth( int[] parents, int bone )
	{
		var depth = 0;
		for ( ; parents[ bone ] >= 0; bone = parents[ bone ] )
			depth++;
		return depth;
	}
}
