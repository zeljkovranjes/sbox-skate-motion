using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

/// <summary>Finds clips by tag. Every motion in the graph definition is indexed once at load.</summary>
internal sealed class MotionCatalog
{
	private readonly List<(AnimationClip Clip, Dictionary<string, string> Tags)> _motions = new();

	internal MotionCatalog( ClipSet clips, IEnumerable<AnimationMotionDefinition> motions )
	{
		foreach ( var motion in motions )
		{
			var clip = clips.Find( motion.Clip );
			if ( clip is null )
				throw new ArgumentException( $"The clip set has no clip named {motion.Clip}." );
			_motions.Add( (clip, new Dictionary<string, string>( motion.Tags, StringComparer.OrdinalIgnoreCase )) );
		}
	}

	internal int Count => _motions.Count;

	/// <summary>Distinct actions of one family, sorted, optionally limited to a style.</summary>
	internal IReadOnlyList<string> Actions( string family, string style = null )
	{
		var actions = new SortedSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var (_, tags) in _motions )
		{
			if ( !Has( tags, "family", family ) || !tags.TryGetValue( "action", out var action ) )
				continue;
			if ( style is not null && !HasWord( tags, "style", style ) )
				continue;
			actions.Add( action );
		}
		return actions.ToList();
	}

	/// <summary>
	/// Adds the clips that have every required tag and the highest preference score. A preference matches a
	/// tag exactly or as one of its hyphen-separated words; an exact "kind" outweighs everything. Ties go to
	/// the motion with the fewest preferred-category tags that were not asked for. Several results are
	/// variants of the same motion. A blend outranks plain clips that match as well, since it is the
	/// complete motion for its activity (its clips mixed by parameters).
	/// </summary>
	internal void Find( string[] keys, string[] values, int required, int count, List<AnimationClip> results )
	{
		results.Clear();
		var best = int.MinValue;
		foreach ( var (clip, tags) in _motions )
		{
			var match = true;
			for ( var i = 0; i < required && match; i++ )
				match = Has( tags, keys[ i ], values[ i ] );
			if ( !match )
				continue;
			var score = clip.Blend is not null ? 1000 : 0;
			for ( var i = required; i < count; i++ )
			{
				var kind = string.Equals( keys[ i ], "kind", StringComparison.OrdinalIgnoreCase );
				if ( string.IsNullOrEmpty( values[ i ] ) )
				{
					if ( !kind && tags.ContainsKey( keys[ i ] ) )
						score -= 1;
					continue;
				}
				if ( Has( tags, keys[ i ], values[ i ] ) )
					score += kind ? 100000 : 100;
				else if ( !kind )
					score += WordScore( tags, keys[ i ], values[ i ] );
			}
			if ( score < best )
				continue;
			if ( score > best )
			{
				results.Clear();
				best = score;
			}
			results.Add( clip );
		}
	}

	// 100 when every word of the tag is the wanted word (e.g. "neutral-neutral" for "neutral"), 50 when only some are.
	private static int WordScore( Dictionary<string, string> tags, string key, string value )
	{
		if ( !tags.TryGetValue( key, out var tag ) )
			return 0;
		var words = tag.Split( '-' );
		var matches = 0;
		foreach ( var word in words )
		{
			if ( string.Equals( word, value, StringComparison.OrdinalIgnoreCase ) )
				matches++;
		}
		return matches == 0 ? 0 : matches == words.Length ? 100 : 50;
	}

	private static bool HasWord( Dictionary<string, string> tags, string key, string value ) => WordScore( tags, key, value ) > 0;

	private static bool Has( Dictionary<string, string> tags, string key, string value ) =>
		tags.TryGetValue( key, out var tag ) && string.Equals( tag, value, StringComparison.OrdinalIgnoreCase );
}
