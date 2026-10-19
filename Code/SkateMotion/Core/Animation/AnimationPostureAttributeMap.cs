namespace SkateMotion.Core.Animation;

/// <summary>Transforms posture-sensitive attribute values using ordered authored name mappings.</summary>
internal sealed class AnimationPostureAttributeMap
{
	private readonly AnimationAttributeKey[] _names;
	private readonly AnimationAttributeKey[] _mapped;
	private readonly AnimationAttributeKey _fallback;

	internal AnimationPostureAttributeMap( ReadOnlySpan<AnimationAttributeKey> names,
		ReadOnlySpan<AnimationAttributeKey> mapped, AnimationAttributeKey fallback )
	{
		if ( names.Length != mapped.Length )
			throw new ArgumentException( "Every authored name needs its mapped value.", nameof( mapped ) );
		_names = names.ToArray();
		_mapped = mapped.ToArray();
		_fallback = fallback;
	}

	internal AnimationAttributeQueryResult Transform( AnimationAttributeQueryResult record )
	{
		var payload = record.Payload;
		if ( record.Kind == 1 )
			return record with { Payload = payload with { A = payload.A ^ 0x80000000u } };
		if ( record.Kind != 3 ) return record;
		var name = new AnimationAttributeKey( payload.A, payload.B, payload.C, payload.D, payload.E );
		var mapped = _fallback;
		for ( var i = 0; i < _names.Length; i++ )
			if ( _names[ i ] == name )
			{
				mapped = _mapped[ i ];
				break;
			}
		return record with { Payload = new( mapped.A, mapped.B, mapped.C, mapped.D, mapped.E, payload.F ) };
	}

	/// <summary>Transforms the entire accumulated list, including records preceding the wrapped child.</summary>
	internal void Transform( AnimationEvaluatedAttributeBuffer output )
	{
		var records = output.MutableValues;
		for ( var i = 0; i < records.Length; i++ )
			records[ i ] = Transform( records[ i ] );
	}
}
