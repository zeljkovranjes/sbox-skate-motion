namespace SkateMotion.Core.Animation;

/// <summary>Retains evaluated records for first-match queries without resampling playback clocks.</summary>
internal sealed class AnimationEvaluatedAttributeCache
{
	private readonly AnimationAttributeQueryResult[] _records;
	private int _count;

	internal AnimationEvaluatedAttributeCache( int capacity )
	{
		if ( capacity < 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_records = new AnimationAttributeQueryResult[ capacity ];
	}

	internal ReadOnlySpan<AnimationAttributeQueryResult> Values => _records.AsSpan( 0, _count );

	internal void Publish( ReadOnlySpan<AnimationAttributeQueryResult> records )
	{
		if ( records.Length > _records.Length )
			throw new InvalidOperationException( "The evaluated attribute cache needs sufficient record capacity." );
		records.CopyTo( _records );
		_count = records.Length;
	}

	internal bool TryGet( AnimationAttributeKey name, ref AnimationAttributeQueryResult result )
	{
		foreach ( var record in Values )
		{
			if ( record.Name != name )
				continue;
			var payload = result.Payload;
			if ( record.Kind is 0 or 2 )
				payload = payload with { A = record.Payload.A };
			else if ( record.Kind == 1 )
				payload = payload with
				{
					A = record.Payload.A, B = record.Payload.B,
					C = record.Payload.C, D = record.Payload.D
				};
			else if ( record.Kind == 3 )
				payload = record.Payload;
			result = record with { Payload = payload };
			return true;
		}
		return false;
	}
}
