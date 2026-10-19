namespace SkateMotion.Core.Animation;

/// <summary>Reusable ordered attribute storage that preserves duplicate identities.</summary>
internal sealed class AnimationEvaluatedAttributeBuffer
{
	private readonly AnimationAttributeQueryResult[] _records;
	private int _count;

	internal AnimationEvaluatedAttributeBuffer( int capacity )
	{
		if ( capacity < 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_records = new AnimationAttributeQueryResult[ capacity ];
	}

	internal ReadOnlySpan<AnimationAttributeQueryResult> Values => _records.AsSpan( 0, _count );
	internal Span<AnimationAttributeQueryResult> MutableValues => _records.AsSpan( 0, _count );
	internal void Clear() => _count = 0;

	internal void Append( AnimationAttributeQueryResult record )
	{
		if ( _count == _records.Length )
			throw new InvalidOperationException( "Evaluated attributes exceed the supplied record capacity." );
		_records[ _count++ ] = record;
	}
}
