namespace SkateMotion.Core.Animation;

/// <summary>
/// The graph's timed clip flags (an underflip's window, a branch out of a trick, a powerslide's early and mid outs, a
/// grab's one-foot and christ early outs) read from the playing clip's own attribute windows, so their timing matches
/// each clip. Windows are fractions of the clip; -1 marks one that lasts the whole clip. A blend reads its heaviest
/// clip that carries attributes, at the blend's phase. Null where no attributes are known, so the caller falls back.
/// </summary>
internal static class ClipFlags
{
	// The flags the clips time, as the graph spells them.
	private static readonly string[] Timed =
		{ "cantranstounderflip", "underflipwindowend", "can-branch", "earlyout", "midout", "one-foot-early-out", "christ-early-out" };

	/// <summary>True for a flag the clips' attribute windows time.</summary>
	internal static bool IsTimed( string flag ) => Array.IndexOf( Timed, flag ) >= 0;

	/// <summary>A name as the attribute bank and the graph both reduce to: lower case, without dashes or underscores.</summary>
	internal static string Normalize( string name )
	{
		var result = new System.Text.StringBuilder( name.Length );
		foreach ( var character in name )
		{
			if ( character is '-' or '_' )
				continue;
			result.Append( char.ToLowerInvariant( character ) );
		}
		return result.ToString();
	}

	/// <summary>Whether the flag's window holds <paramref name="phase"/> (0 to 1 through the clip): false where the clip
	/// has no such window, null without attributes.</summary>
	internal static bool? Active( AnimationClipAttributeSet attributes, string flag, float phase )
	{
		if ( attributes is null )
			return null;
		if ( !attributes.TryWindow( Normalize( flag ), out var start, out var end ) )
			return false;
		return start == -1f || (phase >= start && phase <= end);
	}

	/// <summary>A timed flag of the clip playing <paramref name="time"/> seconds in, or null (not a timed flag, no clip,
	/// or no attributes for it). A flag named after the playing clip, or a blend or clip its blend plays now (a switch's
	/// chosen child, say), is true: each clip carries its own name, and the graph tests it to follow a clip with the one
	/// made to come after it (a landing after its jump).
	/// </summary>
	internal static bool? Active( AnimationClip clip, AnimationParameters parameters, float time, string flag )
	{
		if ( clip is null )
			return null;
		if ( !IsTimed( flag ) )
			return Names( clip, parameters, Normalize( flag ) ) ? true : null;
		return Window( clip, parameters, time, flag );
	}

	/// <summary>Whether the playing clip's own window for <paramref name="flag"/> holds the moment <paramref name="time"/>
	/// seconds in, whatever the flag: false where the clip has no such window, null without a clip or attributes.</summary>
	internal static bool? Window( AnimationClip clip, AnimationParameters parameters, float time, string flag )
	{
		if ( clip is null )
			return null;
		var length = clip.Length( parameters );
		var phase = length > 0f && float.IsFinite( length ) ? time / length : 0f;
		if ( phase > 1f )
			phase = clip.Looping ? phase - MathF.Floor( phase ) : 1f;
		if ( clip.Blend is not { } blend )
			return Active( clip.RawPose?.Clip.Attributes, flag, phase );
		AnimationClipAttributeSet heaviest = null;
		var most = 0f;
		foreach ( var (leaf, weight) in blend.Weights( parameters ) )
		{
			if ( leaf.RawPose?.Clip.Attributes is not { } attributes || weight <= most )
				continue;
			heaviest = attributes;
			most = weight;
		}
		return Active( heaviest, flag, phase );
	}

	// Whether the clip, or a blend or clip its blend plays now, is named <paramref name="flag"/> (normalized).
	private static bool Names( AnimationClip clip, AnimationParameters parameters, string flag ) =>
		Normalize( clip.Name ?? "" ) == flag || (clip.Blend is { } blend && blend.Plays( parameters, flag ));
}
