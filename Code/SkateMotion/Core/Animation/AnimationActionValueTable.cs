using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Looks up six-word identities through explicit buckets, links and an end sentinel.</summary>
internal sealed class AnimationActionValueTable
{
	private readonly int[] _buckets;
	private readonly AnimationActionValueEntry[] _entries;

	// The last bucket holds the end sentinel. -1 is null; callers retain immutable arrays.
	internal AnimationActionValueTable( int[] buckets, AnimationActionValueEntry[] entries )
	{
		ArgumentNullException.ThrowIfNull( buckets );
		ArgumentNullException.ThrowIfNull( entries );
		if ( buckets.Length < 2 )
			throw new ArgumentException( "At least one bucket and its sentinel are required.", nameof( buckets ) );
		foreach ( var entry in buckets )
			ValidateIndex( entry, entries.Length );
		foreach ( var entry in entries )
			ValidateIndex( entry.Next, entries.Length );
		_buckets = buckets;
		_entries = entries;
	}

	internal bool TryGet( InputCommandKey key, out float value )
	{
		var hash = unchecked( key.A + key.B + key.C + key.D + key.E + key.F );
		var bucketCount = _buckets.Length - 1;
		var entry = _buckets[ (int)(hash % (uint)bucketCount) ];
		var visited = 0;
		while ( entry != -1 )
		{
			if ( visited++ == _entries.Length )
				throw new InvalidOperationException( "A value table contains a cyclic chain." );
			var candidate = _entries[ entry ];
			if ( candidate.Key == key )
			{
				if ( entry == _buckets[ bucketCount ] )
					break;
				value = candidate.Value;
				return true;
			}
			entry = candidate.Next;
		}
		value = 0f;
		return false;
	}

	private static void ValidateIndex( int index, int count )
	{
		if ( index < -1 || index >= count )
			throw new ArgumentException( "A table link references an unavailable entry." );
	}
}
