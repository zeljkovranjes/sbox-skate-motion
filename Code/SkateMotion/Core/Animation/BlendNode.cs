using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

/// <summary>
/// A named blend from the graph definition, evaluated each frame from numeric and text parameters:
/// <list type="bullet">
/// <item>"blend": children placed along one parameter (their "At" values, in any order); the two around the
/// parameter's value are mixed, and values beyond the ends hold the end child.</item>
/// <item>"select": the child whose "Key" equals a text parameter, else the "DEFAULT" child, else the first.</item>
/// <item>"switch": the child whose "At" value is nearest a numeric parameter.</item>
/// </list>
/// Children name clips or other blends. Mixed clips play in phase: each runs at its own length scaled to the
/// blend's weighted length, so feet strike together.
/// </summary>
internal sealed class BlendNode
{
	private readonly string _type;
	private readonly string _parameter;
	private (float At, string Key)[] _slots;
	private BlendNode[] _children = Array.Empty<BlendNode>();
	private readonly List<(AnimationClip Clip, float Weight)> _weights = new();
	private SkeletonPose _scratch;

	private BlendNode( string name, string type, string parameter, (float At, string Key)[] slots, AnimationClip clip )
	{
		Name = name;
		_type = type;
		_parameter = parameter;
		_slots = slots;
		Clip = clip;
	}

	internal string Name { get; }

	/// <summary>The clip this node plays when it is a leaf.</summary>
	internal AnimationClip Clip { get; }

	/// <summary>Every clip this node can end in.</summary>
	internal IEnumerable<AnimationClip> Leaves => Clip is not null ? new[] { Clip } : _children.SelectMany( child => child.Leaves ).Distinct();

	/// <summary>Builds the named blends over a clip set; children that name neither a blend nor a clip are an error.</summary>
	internal static Dictionary<string, BlendNode> Build( IReadOnlyDictionary<string, BlendDefinition> definitions, ClipSet clips )
	{
		var nodes = new Dictionary<string, BlendNode>( StringComparer.OrdinalIgnoreCase );
		foreach ( var (name, definition) in definitions )
		{
			var type = (definition.Type ?? "blend").ToLowerInvariant();
			if ( type is not ("blend" or "select" or "switch") )
				throw new ArgumentException( $"Blend {name} has unknown type {definition.Type}." );
			if ( definition.Children.Count == 0 )
				throw new ArgumentException( $"Blend {name} has no children." );
			var slots = definition.Children.Select( ( child, i ) => (child.At ?? (definition.Children.Count == 1 ? 0f : i / (definition.Children.Count - 1f)), child.Key) ).ToArray();
			nodes[ name ] = new BlendNode( name, type, definition.Parameter ?? "", slots, null );
		}
		var leaves = new Dictionary<string, BlendNode>( StringComparer.OrdinalIgnoreCase );
		foreach ( var (name, definition) in definitions )
		{
			nodes[ name ]._children = definition.Children.Select( child =>
			{
				if ( nodes.TryGetValue( child.Name, out var node ) )
					return node;
				if ( leaves.TryGetValue( child.Name, out var leaf ) )
					return leaf;
				var clip = clips.Find( child.Name ) ?? throw new ArgumentException( $"Blend {name} names {child.Name}, which is neither a blend nor a clip." );
				return leaves[ child.Name ] = new BlendNode( child.Name, "clip", "", Array.Empty<(float, string)>(), clip );
			} ).ToArray();
		}
		foreach ( var node in nodes.Values )
		{
			node.CheckCycles( new HashSet<BlendNode>() );
			if ( node._type != "blend" )
				continue;
			// Children may be listed in either direction along the parameter.
			var order = Enumerable.Range( 0, node._slots.Length ).OrderBy( i => node._slots[ i ].At ).ToArray();
			node._slots = order.Select( i => node._slots[ i ] ).ToArray();
			node._children = order.Select( i => node._children[ i ] ).ToArray();
		}
		return nodes;
	}

	private void CheckCycles( HashSet<BlendNode> path )
	{
		if ( !path.Add( this ) )
			throw new ArgumentException( $"Blend {Name} contains itself." );
		foreach ( var child in _children )
			child.CheckCycles( path );
		path.Remove( this );
	}

	/// <summary>Clips and weights (summing to one) for the parameters.</summary>
	internal IReadOnlyList<(AnimationClip Clip, float Weight)> Weights( AnimationParameters parameters )
	{
		_weights.Clear();
		Collect( parameters, 1f, _weights );
		return _weights;
	}

	/// <summary>Weighted length of the mixed clips, seconds.</summary>
	internal float Duration( AnimationParameters parameters )
	{
		var duration = 0f;
		foreach ( var (clip, weight) in Weights( parameters ) )
			duration += clip.Duration * weight;
		return Math.Max( duration, 0.0001f );
	}

	internal void Sample( ClipSkeleton skeleton, AnimationParameters parameters, float time, bool looping, SkeletonPose output )
	{
		var weights = Weights( parameters );
		var duration = 0f;
		foreach ( var (clip, weight) in weights )
			duration += clip.Duration * weight;
		var phase = time / Math.Max( duration, 0.0001f );
		_scratch ??= new SkeletonPose( skeleton.Count );
		var total = 0f;
		foreach ( var (clip, weight) in weights )
		{
			// Resolve wrapping per leaf so a one-shot does not restart while its transition is still fading.
			var clipPhase = looping && clip.Looping ? phase - MathF.Floor( phase ) : Math.Clamp( phase, 0f, 1f );
			if ( total <= 0f )
			{
				output.Sample( clip, skeleton, clipPhase * clip.Duration, false );
				total = weight;
				continue;
			}
			_scratch.Sample( clip, skeleton, clipPhase * clip.Duration, false );
			total += weight;
			output.BlendToward( _scratch, weight / total );
		}
	}

	private void Collect( AnimationParameters parameters, float weight, List<(AnimationClip Clip, float Weight)> output )
	{
		if ( Clip is not null )
		{
			for ( var i = 0; i < output.Count; i++ )
			{
				if ( !ReferenceEquals( output[ i ].Clip, Clip ) )
					continue;
				output[ i ] = (Clip, output[ i ].Weight + weight);
				return;
			}
			output.Add( (Clip, weight) );
			return;
		}
		switch ( _type )
		{
			case "select":
			{
				var key = parameters.Text( _parameter );
				var index = Array.FindIndex( _slots, slot => string.Equals( slot.Key, key, StringComparison.OrdinalIgnoreCase ) );
				if ( index < 0 )
					index = Array.FindIndex( _slots, slot => string.Equals( slot.Key, "DEFAULT", StringComparison.OrdinalIgnoreCase ) );
				_children[ Math.Max( index, 0 ) ].Collect( parameters, weight, output );
				return;
			}
			case "switch":
			{
				var value = parameters.Number( _parameter );
				var best = 0;
				for ( var i = 1; i < _slots.Length; i++ )
				{
					if ( MathF.Abs( _slots[ i ].At - value ) < MathF.Abs( _slots[ best ].At - value ) )
						best = i;
				}
				_children[ best ].Collect( parameters, weight, output );
				return;
			}
			default:
			{
				var value = parameters.Number( _parameter );
				if ( _slots.Length == 1 || value <= _slots[ 0 ].At )
				{
					_children[ 0 ].Collect( parameters, weight, output );
					return;
				}
				for ( var i = 0; i < _slots.Length - 1; i++ )
				{
					if ( value > _slots[ i + 1 ].At )
						continue;
					var span = _slots[ i + 1 ].At - _slots[ i ].At;
					var t = span > 0f ? (value - _slots[ i ].At) / span : 1f;
					if ( t < 1f )
						_children[ i ].Collect( parameters, weight * (1f - t), output );
					if ( t > 0f )
						_children[ i + 1 ].Collect( parameters, weight * t, output );
					return;
				}
				_children[ ^1 ].Collect( parameters, weight, output );
				return;
			}
		}
	}
}
