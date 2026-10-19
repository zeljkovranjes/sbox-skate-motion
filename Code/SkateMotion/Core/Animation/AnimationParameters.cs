using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Named number and text values the graph reads. Setting a value never allocates after first use.</summary>
internal sealed class AnimationParameters
{
	private readonly Dictionary<string, float> _numbers = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<string, string> _texts = new( StringComparer.OrdinalIgnoreCase );

	internal void Set( string name, float value ) => _numbers[ name ] = value;

	internal void Set( string name, bool value ) => _numbers[ name ] = value ? 1f : 0f;

	internal void Set( string name, string value ) => _texts[ name ] = value ?? "";

	internal float Number( string name ) => _numbers.TryGetValue( name, out var value ) ? value : 0f;

	internal bool TryGetNumber( string name, out float value ) => _numbers.TryGetValue( name, out value );

	internal string Text( string name )
	{
		if ( _texts.TryGetValue( name, out var text ) )
			return text;
		return _numbers.TryGetValue( name, out var value ) ? value.ToString( System.Globalization.CultureInfo.InvariantCulture ) : "";
	}

	internal bool Matches( AnimationConditionDefinition condition )
	{
		if ( !_texts.ContainsKey( condition.Parameter ) && float.TryParse( condition.Value,
			System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expected ) )
		{
			var actual = Number( condition.Parameter );
			return condition.Op switch
			{
				"==" => MathF.Abs( actual - expected ) < 0.0001f,
				"!=" => MathF.Abs( actual - expected ) >= 0.0001f,
				">" => actual > expected,
				"<" => actual < expected,
				">=" => actual >= expected,
				"<=" => actual <= expected,
				_ => throw new FormatException( $"Unknown condition operator {condition.Op}." )
			};
		}
		var equal = string.Equals( Text( condition.Parameter ), condition.Value ?? "", StringComparison.OrdinalIgnoreCase );
		return condition.Op switch
		{
			"==" => equal,
			"!=" => !equal,
			_ => throw new FormatException( $"Operator {condition.Op} needs a number." )
		};
	}
}
