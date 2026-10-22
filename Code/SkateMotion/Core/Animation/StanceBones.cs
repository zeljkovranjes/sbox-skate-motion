using System;
using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>The bones a rider stands on the board with: the hips and each leg down to its toes.</summary>
internal static class StanceBones
{
	// The tips of the legs, by the clip skeleton's names: the toes, or the feet without them.
	private static readonly (string Toe, string Foot)[] Tips = { ("left_toe", "left_foot"), ("right_toe", "right_foot") };

	/// <summary>
	/// The hips and every bone between them and each toe (or foot), each once, or none when the skeleton has no hips or no
	/// leg below them.
	/// </summary>
	internal static int[] Legs( ClipSkeleton skeleton )
	{
		var hips = skeleton.IndexOf( "hips" );
		if ( hips < 0 )
			return Array.Empty<int>();
		var bones = new List<int> { hips };
		foreach ( var (toe, foot) in Tips )
		{
			var tip = skeleton.IndexOf( toe ) is var index and >= 0 ? index : skeleton.IndexOf( foot );
			var chain = new List<int>();
			for ( var bone = tip; bone >= 0 && bone != hips; bone = skeleton.Parents[ bone ] )
				chain.Add( bone );
			// A tip that does not hang below the hips is not a leg of theirs.
			if ( tip < 0 || !Below( skeleton, tip, hips ) )
				continue;
			foreach ( var bone in chain )
			{
				if ( !bones.Contains( bone ) )
					bones.Add( bone );
			}
		}
		return bones.Count > 1 ? bones.ToArray() : Array.Empty<int>();
	}

	private static bool Below( ClipSkeleton skeleton, int bone, int ancestor )
	{
		for ( var parent = skeleton.Parents[ bone ]; parent >= 0; parent = skeleton.Parents[ parent ] )
		{
			if ( parent == ancestor )
				return true;
		}
		return false;
	}
}
