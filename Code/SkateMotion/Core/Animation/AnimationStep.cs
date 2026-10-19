using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

/// <summary>Runtime form of one step: resolves its tag query and samples the chosen clip or blend.</summary>
internal sealed class AnimationStep
{
	private readonly string[] _keys;
	private readonly int _required;
	private readonly string[] _templates;
	private readonly string[] _values;
	private readonly string _blendParameter;
	private readonly int _blendSlot;
	private readonly (float At, string Tag)[] _points;
	private readonly AnimationClip[][] _candidates;
	private readonly List<AnimationClip> _scratch = new();
	private readonly AnimationClip[] _chosen;

	internal AnimationStep( AnimationStepDefinition definition )
	{
		Optional = definition.Optional;
		var blend = !string.IsNullOrEmpty( definition.BlendParameter ) && definition.BlendPoints.Count > 0;
		_required = definition.Select.Count + (blend ? 1 : 0);
		var count = _required + definition.Prefer.Count;
		_keys = new string[ count ];
		_templates = new string[ count ];
		_values = new string[ count ];
		var i = 0;
		foreach ( var (key, value) in definition.Select )
		{
			_keys[ i ] = key;
			_templates[ i++ ] = value;
		}
		_blendSlot = blend ? i++ : -1;
		foreach ( var (key, value) in definition.Prefer )
		{
			_keys[ i ] = key;
			_templates[ i++ ] = value;
		}
		if ( blend )
		{
			_keys[ _blendSlot ] = definition.BlendTag;
			_blendParameter = definition.BlendParameter;
			_points = definition.BlendPoints.Select( point => (point.Value, point.Key) ).OrderBy( point => point.Item1 ).ToArray();
		}
		else
		{
			_points = new[] { (0f, (string)null) };
		}
		_candidates = new AnimationClip[ _points.Length ][];
		_chosen = new AnimationClip[ _points.Length ];
	}

	internal bool Available { get; private set; }

	internal bool Optional { get; }

	/// <summary>Name of the chosen clip (the first blend point's when blending).</summary>
	internal string ClipName => _chosen[ 0 ]?.Name;

	/// <summary>True when every blend point would choose the same clips as now for <paramref name="variant"/>.</summary>
	internal bool Keeps( int variant )
	{
		for ( var p = 0; p < _points.Length; p++ )
		{
			var options = _candidates[ p ];
			var next = options.Length > 0 ? options[ variant % options.Length ] : null;
			if ( !ReferenceEquals( next, _chosen[ p ] ) )
				return false;
		}
		return true;
	}

	/// <summary>True when this step has a motion to play.</summary>
	internal bool Playable => _chosen[ 0 ] is not null;

	/// <summary>Re-resolves the query; returns 2 when a required tag changed, 1 when only preferences did, else 0.</summary>
	internal int Resolve( AnimationParameters parameters, MotionCatalog catalog )
	{
		var level = _candidates[ 0 ] is null ? 2 : 0;
		for ( var i = 0; i < _keys.Length; i++ )
		{
			if ( i == _blendSlot )
				continue;
			var template = _templates[ i ];
			var value = template.StartsWith( '$' ) ? parameters.Text( template[ 1.. ] ) : template;
			if ( !string.Equals( value, _values[ i ], StringComparison.OrdinalIgnoreCase ) )
			{
				_values[ i ] = value;
				level = Math.Max( level, i < _required ? 2 : 1 );
			}
		}
		if ( level == 0 )
			return 0;
		Available = true;
		for ( var p = 0; p < _points.Length; p++ )
		{
			if ( _blendSlot >= 0 )
				_values[ _blendSlot ] = _points[ p ].Tag;
			catalog.Find( _keys, _values, _required, _keys.Length, _scratch );
			_candidates[ p ] = _scratch.ToArray();
			Available &= _scratch.Count > 0 || Optional;
		}
		return level;
	}

	/// <summary>Chooses one variant per blend point; successive entries rotate through the variants.</summary>
	internal void Choose( int variant )
	{
		for ( var p = 0; p < _points.Length; p++ )
		{
			var options = _candidates[ p ];
			_chosen[ p ] = options.Length > 0 ? options[ variant % options.Length ] : null;
		}
	}

	internal float Duration( AnimationParameters parameters )
	{
		if ( !Playable )
			return 0f;
		if ( _chosen.Length == 1 )
			return _chosen[ 0 ]?.Length( parameters ) ?? 0f;
		var (a, b, weight) = Neighbours( parameters.Number( _blendParameter ) );
		return Lerp( _chosen[ a ].Length( parameters ), _chosen[ b ].Length( parameters ), weight );
	}

	internal void Sample( ClipSkeleton skeleton, AnimationParameters parameters, float time, bool looping,
		SkeletonPose output, SkeletonPose scratch )
	{
		if ( _chosen.Length == 1 )
		{
			output.Sample( _chosen[ 0 ], skeleton, time, looping, parameters );
			return;
		}
		var (a, b, weight) = Neighbours( parameters.Number( _blendParameter ) );
		var phase = time / Math.Max( Duration( parameters ), 0.0001f );
		output.Sample( _chosen[ a ], skeleton, phase * _chosen[ a ].Length( parameters ), looping, parameters );
		if ( weight <= 0f || a == b )
			return;
		scratch.Sample( _chosen[ b ], skeleton, phase * _chosen[ b ].Length( parameters ), looping, parameters );
		output.BlendToward( scratch, weight );
	}

	private (int A, int B, float Weight) Neighbours( float value )
	{
		if ( value <= _points[ 0 ].At )
			return (0, 0, 0f);
		for ( var i = 0; i < _points.Length - 1; i++ )
		{
			if ( value <= _points[ i + 1 ].At )
				return (i, i + 1, (value - _points[ i ].At) / Math.Max( _points[ i + 1 ].At - _points[ i ].At, 0.0001f ));
		}
		var last = _points.Length - 1;
		return (last, last, 0f);
	}

	private static float Lerp( float a, float b, float t ) => a + (b - a) * t;
}
