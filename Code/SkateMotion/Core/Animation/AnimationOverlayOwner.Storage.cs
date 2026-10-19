namespace SkateMotion.Core.Animation;

internal sealed partial class AnimationOverlayOwner
{
	/// <summary>Clears keyed channels first, then releases tree storage in its retained array order.</summary>
	internal void Reset()
	{
		Array.Clear( _buckets );
		_count = 0;
		_freeEntryCount = 0;
		foreach ( var entry in _entries )
		{
			entry.Priority = 0;
			entry.Previous = null;
			entry.Next = null;
			entry.HashNext = null;
			entry.Owner?.ForgetTrees();
			_freeEntries[ _freeEntryCount++ ] = entry.Index;
		}
		_freeTreeCount = 0;
		for ( var slot = 0; slot < _trees.Length; slot++ )
		{
			if ( _trees[ slot ] is not null )
				_factory.Release( slot, _trees[ slot ] );
			_trees[ slot ] = null;
			_freeTrees[ _freeTreeCount++ ] = slot;
		}
		_head = null;
	}

	private IAnimationPlaybackTree CreateTree( AnimationChannelReplacement request )
	{
		if ( _freeTreeCount == 0 )
			throw new InvalidOperationException( "The initialized overlay storage pool is exhausted." );
		var slot = _freeTrees[ _freeTreeCount - 1 ];
		var tree = _factory.Create( slot, request );
		_trees[ slot ] = tree;
		return tree;
	}

	private void CommitTree( IAnimationPlaybackTree tree )
	{
		if ( _freeTreeCount == 0 || !ReferenceEquals( _trees[ _freeTrees[ _freeTreeCount - 1 ] ], tree ) )
			throw new InvalidOperationException( "Overlay construction must commit the current free storage slot." );
		_freeTreeCount--;
	}

	private void ReleaseTree( IAnimationPlaybackTree tree )
	{
		for ( var slot = 0; slot < _trees.Length; slot++ )
		{
			if ( !ReferenceEquals( _trees[ slot ], tree ) )
				continue;
			_factory.Release( slot, tree );
			_trees[ slot ] = null;
			_freeTrees[ _freeTreeCount++ ] = slot;
			return;
		}
		throw new InvalidOperationException( "The overlay tree does not belong to the retained storage pool." );
	}
}
