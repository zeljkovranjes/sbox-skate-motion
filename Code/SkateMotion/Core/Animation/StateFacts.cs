using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>What the host knows this frame, as a state graph's tests read it.</summary>
internal sealed class StateFacts
{
	/// <summary>Active intents and their magnitudes (1 for plain on/off intents).</summary>
	internal Dictionary<string, float> Intents { get; } = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Other named facts; a fact is true when present and nonzero.</summary>
	internal Dictionary<string, float> Facts { get; } = new( StringComparer.OrdinalIgnoreCase );

	private readonly Dictionary<string, Dictionary<string, string>> _arguments = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Supplies a named argument for a categorical fact, such as the current gait.</summary>
	internal void SetArgument( string fact, string argument, string value )
	{
		if ( !_arguments.TryGetValue( fact, out var arguments ) )
		{
			arguments = new( StringComparer.OrdinalIgnoreCase );
			_arguments.Add( fact, arguments );
		}
		arguments[ argument ] = value;
	}

	/// <summary>Requires every authored category to match; numeric comparisons are evaluated separately.</summary>
	internal bool MatchesArguments( StateTestDefinition test )
	{
		if ( test.Arguments.Count == 0 || test.Compare.Count > 0 )
			return true;
		if ( !_arguments.TryGetValue( test.Fact ?? "", out var arguments ) )
			return false;
		foreach ( var (name, expected) in test.Arguments )
		{
			if ( !arguments.TryGetValue( name, out var actual ) || !string.Equals( actual, expected, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}
		return true;
	}

	/// <summary>Physical situation: ground, air, grind, offboard, offboardair, wipeout.</summary>
	internal string Physics { get; set; } = "ground";

	/// <summary>Current grind name, or empty.</summary>
	internal string Grind { get; set; } = "";

	/// <summary>Forward speed in metres per second; negative rolling backwards.</summary>
	internal float Speed { get; set; }

	/// <summary>The rider's pose is mirrored relative to the clips.</summary>
	internal bool Mirrored { get; set; }

	/// <summary>The rider stands in their other stance.</summary>
	internal bool Switch { get; set; }

	/// <summary>The rider has no board.</summary>
	internal bool NoBoard { get; set; }

	internal void Clear()
	{
		Intents.Clear();
		Facts.Clear();
		foreach ( var arguments in _arguments.Values )
			arguments.Clear();
		Grind = "";
	}
}
