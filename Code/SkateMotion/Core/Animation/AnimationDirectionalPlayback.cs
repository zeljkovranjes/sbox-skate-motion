namespace SkateMotion.Core.Animation;

/// <summary>Directional playback over authored child sample positions and retained selection state.</summary>
internal sealed class AnimationDirectionalPlayback : IAnimationRatePlaybackNode
{
	private readonly IAnimationRatePlaybackNode[] _children;
	private readonly float[] _samples;
	private readonly int[] _order;
	private float _parameter = float.MaxValue;
	private bool _normalized = true;

	internal AnimationDirectionalPlayback( IAnimationRatePlaybackNode[] children, float[] samples )
	{
		if ( children.Length < 2 || children.Length != samples.Length )
			throw new ArgumentException( "Directional playback needs at least two matching children and samples." );
		_children = new IAnimationRatePlaybackNode[ children.Length ];
		_samples = new float[ samples.Length ];
		_order = new int[ children.Length ];
		Array.Copy( children, _children, children.Length );
		Array.Copy( samples, _samples, samples.Length );
		for ( var index = 0; index < _order.Length; index++ )
			_order[ index ] = index;
		SecondIndex = 1;
		Duration = _children[ 0 ].Duration;
	}

	/// <summary>Cached duration of the selected pair.</summary>
	public float Duration { get; private set; }

	/// <summary>Cached phase expressed in the selected pair's duration.</summary>
	public float Elapsed { get; private set; }

	internal int FirstIndex { get; private set; }
	internal int SecondIndex { get; private set; }
	internal float Weight { get; private set; }
	internal bool FirstIsEndpoint => ReferenceEquals( _children[ FirstIndex ], _children[ _order[ 0 ] ] );
	internal bool SecondIsEndpoint => ReferenceEquals( _children[ SecondIndex ], _children[ _order[ _order.Length - 1 ] ] );

	internal bool UpdateParameter( float value, bool normalized, bool present = true )
	{
		if ( !SetParameter( value, normalized, present ) )
			return false;
		SelectPair();
		return true;
	}

	internal bool UpdateParameters( IAnimationDirectionalParameters parameters,
		float value, bool normalized, bool present = true )
	{
		ArgumentNullException.ThrowIfNull( parameters );
		var childrenChanged = false;
		for ( var index = 0; index < _order.Length; index++ )
			childrenChanged |= parameters.UpdateChild( _order[ index ] );

		var parameterChanged = SetParameter( value, normalized, present );
		if ( !childrenChanged && !parameterChanged )
			return false;

		for ( var index = 0; index < _order.Length; index++ )
		{
			var child = _order[ index ];
			_samples[ child ] = parameters.QuerySample( child );
		}
		SelectPair();
		return true;
	}

	private bool SetParameter( float value, bool normalized, bool present )
	{
		if ( !present || (_normalized == normalized && MathF.Abs( value - _parameter ) < 0.0001f) )
			return false;
		_parameter = value;
		_normalized = normalized;
		return true;
	}

	private void SelectPair()
	{
		var value = _parameter;
		SortSamples();
		if ( _normalized )
		{
			var first = _samples[ _order[ 0 ] ];
			var last = _samples[ _order[ _order.Length - 1 ] ];
			value = MathF.FusedMultiplyAdd( value, last - first, first );
		}

		var upper = LowerBound( value );
		if ( upper == _order.Length )
			upper--;
		if ( upper == 0 )
			upper++;
		var firstIndex = _order[ upper - 1 ];
		var secondIndex = _order[ upper ];
		var lowerSample = _samples[ firstIndex ];
		var upperSample = _samples[ secondIndex ];
		var clamped = lowerSample - value >= 0f ? lowerSample : value;
		clamped = upperSample - clamped >= 0f ? clamped : upperSample;
		var interval = upperSample - lowerSample;
		var divisor = -MathF.Abs( interval ) >= 0f ? 1f : interval;
		var weight = ( clamped - lowerSample ) / divisor;
		weight = -weight >= 0f ? 0f : weight;
		Weight = 1f - weight >= 0f ? weight : 1f;

		var previousDuration = -Duration >= 0f ? 1f : Duration;
		var phase = Elapsed / previousDuration;
		FirstIndex = firstIndex;
		SecondIndex = secondIndex;
		RefreshDuration();
		Seek( phase * Duration );
	}

	/// <summary>Advances both active children and scales the first child's completion back to pair time.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		var first = _children[ FirstIndex ];
		var second = _children[ SecondIndex ];
		var firstDuration = first.Duration;
		var secondDuration = second.Duration;
		var duration = Duration;
		var fraction = delta / duration;
		first.Advance( firstDuration * fraction, phase, ref completion );
		var ignored = AnimationPlaybackCompletion.Empty;
		second.Advance( secondDuration * fraction, phase, ref ignored );
		var elapsed = first.Elapsed;
		var inverse = 1f / firstDuration;
		Elapsed = duration * ( elapsed * inverse );
		completion = completion with
		{
			Remaining = duration * ( inverse * completion.Remaining ),
			UnusedDelta = duration * ( inverse * completion.UnusedDelta )
		};
	}

	/// <summary>Seeks both active children to the requested pair phase.</summary>
	public void Seek( float time )
	{
		var phase = time / Duration;
		var first = _children[ FirstIndex ];
		first.Seek( phase * first.Duration );
		var second = _children[ SecondIndex ];
		second.Seek( phase * second.Duration );
		Elapsed = time;
	}

	/// <summary>Changes every child rate, then restores the pair's prior phase.</summary>
	public void SetRate( float rate )
	{
		var duration = Duration;
		var elapsed = Elapsed;
		var divisor = -duration >= 0f ? 1f : duration;
		for ( var index = 0; index < _order.Length; index++ )
			_children[ _order[ index ] ].SetRate( rate );
		RefreshDuration();
		Seek( ( elapsed / divisor ) * Duration );
	}

	private void RefreshDuration()
	{
		var first = _children[ FirstIndex ].Duration * ( 1f - Weight );
		Duration = MathF.FusedMultiplyAdd( _children[ SecondIndex ].Duration, Weight, first );
	}

	private int LowerBound( float value )
	{
		var start = 0;
		var count = _order.Length;
		while ( count > 0 )
		{
			var half = count / 2;
			var middle = start + half;
			if ( _samples[ _order[ middle ] ] >= value )
				count = half;
			else
			{
				start = middle + 1;
				count -= half + 1;
			}
		}
		return start;
	}

	private void SortSamples()
	{
		var end = _order.Length - 1;
		while ( end > 0 )
		{
			var lastSwap = 0;
			for ( var index = 0; index < end; index++ )
			{
				if ( _samples[ _order[ index + 1 ] ] >= _samples[ _order[ index ] ] )
					continue;
				(_order[ index ], _order[ index + 1 ]) = (_order[ index + 1 ], _order[ index ]);
				lastSwap = index;
			}
			end = lastSwap;
		}
	}
}
