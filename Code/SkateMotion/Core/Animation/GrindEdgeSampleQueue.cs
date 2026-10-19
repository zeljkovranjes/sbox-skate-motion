namespace SkateMotion.Core.Animation;

/// <summary>Selects and removes contact samples while preserving the order of all remaining samples.</summary>
internal sealed class GrindEdgeSampleQueue
{
	private readonly GrindEdgeSelectionCurve _curve;
	private readonly GrindEdgeSelectionSettings _settings;
	private readonly GrindEdgeSample[] _samples = new GrindEdgeSample[ GrindEdgeQuery.Capacity ];
	internal int RemainingCount { get; private set; }
	internal int SourceEdgeCount { get; private set; }

	/// <summary>Releases pending samples after scoring without replacing the source-edge count.</summary>
	internal void Clear() => RemainingCount = 0;

	internal GrindEdgeSampleQueue( GrindEdgeSelectionCurve curve, GrindEdgeSelectionSettings settings )
	{
		ArgumentNullException.ThrowIfNull( curve );
		_curve = curve;
		_settings = settings;
	}

	internal void SetSamples( GrindEdgeSample[] samples )
	{
		ArgumentNullException.ThrowIfNull( samples );
		if ( samples.Length > _samples.Length )
			throw new ArgumentException( "At most forty pending contact samples are supported." );
		Array.Copy( samples, _samples, samples.Length );
		RemainingCount = samples.Length;
		SourceEdgeCount = samples.Length;
	}

	/// <summary>Replaces pending samples from the supplied edge prefix, retaining each successful source index.</summary>
	internal void Populate( GrindEdgeContactSampler sampler, GrindEdgeTrajectory trajectory,
		GrindEdgeRecord[] edges, int count )
	{
		ArgumentNullException.ThrowIfNull( sampler );
		ArgumentNullException.ThrowIfNull( edges );
		if ( count < 0 || count > edges.Length || count > _samples.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		RemainingCount = 0;
		SourceEdgeCount = count;
		for ( var i = 0; i < count; i++ )
		{
			var sample = default( GrindEdgeSample );
			if ( sampler.Sample( trajectory, edges[ i ].Segment, ref sample ) )
				_samples[ RemainingCount++ ] = sample with { EdgeIndex = i };
		}
	}

	/// <summary>Empty queues preserve output; an unsuccessful nonempty scan clears only its validity.</summary>
	internal bool Next( float separationThreshold, ref GrindEdgeSample output )
	{
		if ( RemainingCount == 0 )
			return false;
		var selected = -1;
		var separation = _settings.InitialSeparation;
		var height = _settings.InitialHeight;
		var angle = _settings.InitialAngle;
		for ( var i = 0; i < RemainingCount; i++ )
		{
			var sample = _samples[ i ];
			if ( separationThreshold <= sample.Separation )
			{
				if ( !(sample.Separation >= separation) )
				{
					separation = sample.Separation;
					selected = i;
				}
				continue;
			}
			var margin = _curve.Evaluate( sample.Point.Y - height );
			if ( angle - sample.Angle <= margin )
				continue;
			angle = sample.Angle;
			height = sample.Point.Y;
			separation = -1f;
			selected = i;
		}
		if ( selected == -1 )
		{
			output = output with { IsValid = false };
			return false;
		}
		// Validity belongs to the sample producer; selection does not filter or replace it.
		output = _samples[ selected ];
		RemainingCount--;
		for ( var i = selected; i < RemainingCount; i++ )
			_samples[ i ] = _samples[ i + 1 ];
		return true;
	}
}
