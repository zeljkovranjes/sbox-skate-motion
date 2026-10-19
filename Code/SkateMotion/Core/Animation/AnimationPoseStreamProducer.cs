namespace SkateMotion.Core.Animation;

/// <summary>Emits authored pose commands using the same clock as skeletal clip preparation.</summary>
internal sealed class AnimationPoseStreamProducer
{
	private const float TimeScale = 1f / 8191f;
	private readonly AnimationClipClock _clock;
	private readonly AnimationPoseStreamTrack[] _tracks;

	internal AnimationPoseStreamProducer( AnimationClipClock clock, AnimationPoseStreamTrack[] tracks )
	{
		ArgumentNullException.ThrowIfNull( clock );
		ArgumentNullException.ThrowIfNull( tracks );
		_clock = clock;
		_tracks = new AnimationPoseStreamTrack[ tracks.Length ];
		for ( var index = 0; index < tracks.Length; index++ )
		{
			ArgumentNullException.ThrowIfNull( tracks[ index ] );
			_tracks[ index ] = tracks[ index ];
		}
	}

	internal void Sample( AnimationPoseStreamContext context )
	{
		ArgumentNullException.ThrowIfNull( context );
		var elapsed = _clock.Elapsed;
		var supplied = 0;
		foreach ( var track in _tracks )
		{
			var slot = context.Register( track.Name, track.PairedName );
			var keys = track.Keys;
			for ( var index = 0; index < keys.Length; index++ )
			{
				var time = ( keys[ index ].Time * TimeScale ) * _clock.Duration;
				if ( time < elapsed )
					continue;
				if ( index == 0 )
					context.Append( slot, (uint)keys[ index ].Value << 23 );
				else
					AppendInterval( context, slot, keys[ index - 1 ], keys[ index ], time, elapsed );
				supplied |= 1 << slot;
				break;
			}
		}
		for ( var slot = 0; slot < AnimationPoseStreamContext.SlotCount; slot++ )
			if ( ( supplied & ( 1 << slot ) ) == 0 )
				context.Append( slot, 0 );
	}

	private void AppendInterval( AnimationPoseStreamContext context, int slot,
		AnimationPoseStreamKeyframe previous, AnimationPoseStreamKeyframe current, float time, float elapsed )
	{
		context.Append( slot, (uint)previous.Value << 23 );
		context.Append( slot, (uint)current.Value << 23 );
		var denominator = MathF.FusedMultiplyAdd( previous.Time * TimeScale, _clock.Duration, -time );
		var fraction = ( time - elapsed ) / denominator;
		var scaled = MathF.FusedMultiplyAdd( fraction, AnimationPoseStreamContext.WeightMaximum,
			AnimationPoseStreamContext.WeightMaximum );
		context.Append( slot, 0x40000000u | unchecked( (uint)(long)scaled ) );
		context.Combine( slot );
	}
}
