namespace SkateMotion.Core.Input;

using Vector2 = System.Numerics.Vector2;

/// <summary>Advances ordered stick paths and preserves winner and hold timing.</summary>
internal sealed class GesturePatternMatcher
{
	private readonly GesturePattern[] _patterns;
	private readonly GesturePatternNode[] _nodes;
	private readonly byte _maximumGap;
	private int _samples;
	private bool _locked;
	private int _held = -1;

	internal int HeldPattern { get; private set; } = -1;
	internal int Count => _nodes.Length;
	internal GesturePatternNode Node( int index ) => _nodes[ index ];

	internal GesturePatternMatcher( GesturePattern[] patterns, byte maximumGap )
	{
		ArgumentNullException.ThrowIfNull( patterns );
		_patterns = patterns.AsSpan().ToArray();
		_nodes = new GesturePatternNode[ patterns.Length ];
		_maximumGap = maximumGap;
		for ( var index = 0; index < _patterns.Length; index++ )
		{
			ArgumentNullException.ThrowIfNull( _patterns[ index ] );
			_nodes[ index ] = new( _patterns[ index ].Count );
		}
	}

	internal bool Step( Vector2 sample, int mode, out GesturePatternMatch match )
	{
		if ( !float.IsFinite( sample.X ) || !float.IsFinite( sample.Y ) )
			throw new ArgumentException( "Stick coordinates must be finite.", nameof( sample ) );
		match = default;
		HeldPattern = -1;
		if ( _held >= 0 )
		{
			var pattern = _patterns[ _held ];
			var distance = GesturePatternNode.DistanceSquared( sample, pattern[ pattern.Count - 1 ] );
			if ( distance - pattern.SquaredTolerance <= 0f )
				HeldPattern = _held;
			else
				_held = -1;
		}
		_samples = Math.Min( _samples + 1, 2 );
		if ( _locked )
		{
			for ( var index = 0; index < _nodes.Length; index++ )
				_nodes[ index ].Reset();
			_locked = false;
			return false;
		}
		if ( _samples < 2 )
			return false;

		var winner = -1;
		var score = 0f;
		for ( var index = 0; index < _nodes.Length; index++ )
		{
			_nodes[ index ].Advance( _patterns[ index ], sample, _maximumGap );
			var node = _nodes[ index ];
			if ( !node.Complete )
				continue;
			var count = (float)node.Count;
			var error = Math.Max( 0.15f, node.Error );
			var divisor = Math.Min( error / count, 0.15f ) * node.Span;
			var candidate = count * count * count * count / divisor;
			if ( winner >= 0 && candidate <= score )
				continue;
			winner = index;
			score = candidate;
		}
		if ( winner < 0 )
		{
			match = new( -1, 0f, 1000000f, 0f );
			return false;
		}

		var selected = _nodes[ winner ];
		var ratio = (float)selected.Span / selected.Count;
		var lower = mode == 2 ? 1.5f : 1.75f;
		var upper = mode == 2 ? 3f : 4.4f;
		var quality = 0f;
		if ( ratio <= lower )
			quality = 1f;
		else if ( ratio < upper )
		{
			var slope = 1f / (lower - upper);
			var scaled = ratio * slope;
			quality = (float)((double)scaled - (double)slope * upper);
		}
		match = new( winner, quality, selected.Error, selected.Span );
		_held = winner;
		_locked = true;
		return true;
	}
}
