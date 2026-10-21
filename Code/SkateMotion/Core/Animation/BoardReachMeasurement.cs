using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Measures where each reach clip holds the board: the bearing of the clip's board bone from its root bone as the hold
/// starts (the first frame of the cycle clip), in the root's own frame (+X forward, +Y left) as the rider is shown,
/// against the blend yaw the clip carries. Null when the clips have no board bone or no yaw for every reach.
/// </summary>
internal static class BoardReachMeasurement
{
	internal static BoardReachMap Measure( RawAnimationClipSet clips, string anchor, string yawAttribute,
		params RawAnimationClip[][] cycles )
	{
		var board = string.IsNullOrEmpty( anchor ) ? -1 : Array.FindIndex( clips.Names,
			name => string.Equals( name, anchor, StringComparison.OrdinalIgnoreCase ) );
		if ( board <= 0 )
			return null;
		var key = ClipFlags.Normalize( yawAttribute ?? "" );
		var points = new List<(float Yaw, float Bearing)>();
		var locals = new AnimationBoneTransform[ clips.BoneCount ];
		foreach ( var leaves in cycles )
		{
			foreach ( var leaf in leaves )
			{
				if ( Yaw( leaf, key ) is not { } yaw || points.Exists( point => point.Yaw == yaw ) )
					continue;
				var clip = clips.CreateReferencedClip( leaf.Name );
				if ( clip is null )
					return null;
				clip.Sample( 0f, false, locals );
				var root = Position( clips, locals, 0 );
				var held = Vector3.Transform( Position( clips, locals, board ) - root, Quaternion.Conjugate( locals[ 0 ].Rotation ) );
				points.Add( (yaw, MathF.Atan2( held.Y, held.X ) * 180f / MathF.PI) );
			}
		}
		if ( points.Count < 2 )
			return null;
		points.Sort( ( a, b ) => a.Yaw.CompareTo( b.Yaw ) );
		try
		{
			return new BoardReachMap( points.ConvertAll( point => point.Yaw ).ToArray(),
				points.ConvertAll( point => point.Bearing ).ToArray() );
		}
		catch ( ArgumentException )
		{
			// Reaches that do not sweep round in order cannot be inverted; the yaw is then used as it comes.
			return null;
		}
	}

	private static float? Yaw( RawAnimationClip leaf, string key )
	{
		if ( leaf.Attributes is null )
			return null;
		foreach ( var record in leaf.Attributes.Records )
		{
			if ( ClipFlags.Normalize( record.Name.Decode() ) == key )
				return record.Payload.Scalar;
		}
		return null;
	}

	// A bone's position in the clip frame, from its local transforms down the chain.
	private static Vector3 Position( RawAnimationClipSet clips, ReadOnlySpan<AnimationBoneTransform> locals, int bone )
	{
		var position = Vector3.Zero;
		var chain = new List<int>();
		for ( var at = bone; at >= 0; at = clips.Parents[ at ] )
			chain.Add( at );
		var rotation = Quaternion.Identity;
		for ( var i = chain.Count - 1; i >= 0; i-- )
		{
			var local = locals[ chain[ i ] ];
			position += Vector3.Transform( new Vector3( local.Position.X, local.Position.Y, local.Position.Z ), rotation );
			rotation = Quaternion.Normalize( rotation * local.Rotation );
		}
		return position;
	}
}
