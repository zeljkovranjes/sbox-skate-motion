using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Owns keyed overlay channels, ordered lookup buckets and the effective animation root.</summary>
internal sealed partial class AnimationOverlayOwner
{
	private readonly IAnimationOverlayTreeFactory _factory;
	private readonly Entry[] _entries;
	private Entry[] _buckets;
	private Entry[] _rehash;
	private int _bucketCount = 1;
	private int _resizeThreshold;
	private readonly int[] _freeEntries;
	private readonly IAnimationPlaybackTree[] _trees;
	private readonly int[] _freeTrees;
	private int _freeEntryCount;
	private int _freeTreeCount;
	private int _count;
	private Entry _head;

	internal AnimationOverlayOwner( int channelCapacity, IAnimationOverlayTreeFactory factory )
	{
		ArgumentNullException.ThrowIfNull( factory );
		if ( channelCapacity < 1 || channelCapacity > int.MaxValue / 2 )
			throw new ArgumentOutOfRangeException( nameof( channelCapacity ) );
		var treeCapacity = channelCapacity * 2;
		var maximumBuckets = 2;
		while ( (int)(float)maximumBuckets < channelCapacity )
			maximumBuckets = AnimationOverlayBucketGrowth.Next( maximumBuckets, (int)(float)maximumBuckets + 1 );
		_factory = factory;
		_entries = new Entry[ channelCapacity ];
		_buckets = new Entry[ maximumBuckets ];
		_rehash = new Entry[ maximumBuckets ];
		_freeEntries = new int[ channelCapacity ];
		_trees = new IAnimationPlaybackTree[ treeCapacity ];
		_freeTrees = new int[ treeCapacity ];
		for ( var i = 0; i < channelCapacity; i++ )
		{
			_entries[ i ] = new Entry( this, i );
			_freeEntries[ i ] = i;
		}
		for ( var i = 0; i < treeCapacity; i++ )
			_freeTrees[ i ] = i;
		_freeEntryCount = channelCapacity;
		_freeTreeCount = treeCapacity;
	}

	internal IAnimationPosePlaybackNode BaseRoot { get; private set; }
	internal IAnimationPosePlaybackNode Root => _head is null ? BaseRoot : _head.Node;
	internal int Count => _count;
	internal int FreeTreeCount => _freeTreeCount;
	internal int BucketCount => _bucketCount;

	/// <summary>Publishes a changed base root and updates only the last linked channel's base child.</summary>
	internal void PublishBase( IAnimationPosePlaybackNode node )
	{
		BaseRoot = node;
		var tail = _head;
		if ( tail is null )
			return;
		while ( tail.Next is not null )
			tail = tail.Next;
		tail.Base.Target = node;
	}

	internal AnimationOverlayRequestResult Request( InputCommandKey channel, int priority,
		bool allowCreate, AnimationChannelReplacement request )
	{
		var entry = Find( channel );
		if ( entry is not null )
			return (AnimationOverlayRequestResult)entry.Owner.Replace( request );
		if ( !allowCreate )
			return AnimationOverlayRequestResult.MissingChannel;
		return Create( channel, priority, request );
	}

	/// <summary>Creates a missing channel without replacing an existing key.</summary>
	internal AnimationOverlayRequestResult Create( InputCommandKey channel, int priority,
		AnimationChannelReplacement request )
	{
		if ( Find( channel ) is not null )
			return AnimationOverlayRequestResult.MissingChannel;
		if ( _freeEntryCount == 0 )
			return AnimationOverlayRequestResult.Capacity;
		var entry = _entries[ _freeEntries[ _freeEntryCount - 1 ] ];
		var tree = CreateTree( request );
		if ( tree is null )
			return AnimationOverlayRequestResult.MissingTree;
		if ( tree.Root is not IAnimationPosePlaybackNode )
			throw new InvalidOperationException( "Overlay trees must support pose preparation." );
		entry.Base.Target = Root;
		entry.Initialize( tree );
		entry.Blend.StartEnvelope( request.RecoveryDuration, request.ExitDuration, request.Flags );
		_freeEntryCount--;
		CommitTree( tree );
		entry.Key = channel;
		GrowHash( _count + 1 );
		var bucket = Bucket( channel );
		entry.HashNext = _buckets[ bucket ];
		_buckets[ bucket ] = entry;
		_count++;
		Insert( entry, priority );
		return AnimationOverlayRequestResult.Success;
	}

	internal bool Stop( InputCommandKey channel )
	{
		var entry = Find( channel );
		if ( entry is null )
			return false;
		entry.Blend.RequestStop();
		return true;
	}

	/// <summary>Promotes eligible pending trees in hash bucket and chain order.</summary>
	internal void PromoteCompleted()
	{
		for ( var bucket = 0; bucket < _bucketCount; bucket++ )
			for ( var entry = _buckets[ bucket ]; entry is not null; entry = entry.HashNext )
				entry.Owner.CleanupCompleted();
	}

	/// <summary>Removes finished envelopes in lookup order before the next frame advances.</summary>
	internal void RemoveFinished()
	{
		for ( var bucket = 0; bucket < _bucketCount; bucket++ )
		{
			var entry = _buckets[ bucket ];
			while ( entry is not null )
			{
				var next = entry.HashNext;
				if ( entry.Blend.Finished )
					Remove( entry.Key );
				entry = next;
			}
		}
	}

	private int Bucket( InputCommandKey key ) =>
		(int)(unchecked( key.A + key.B + key.C + key.D + key.E + key.F ) % (uint)_bucketCount);

	private void GrowHash( int count )
	{
		if ( count <= _resizeThreshold )
			return;
		var previousCount = _bucketCount;
		_bucketCount = AnimationOverlayBucketGrowth.Next( previousCount, count );
		_resizeThreshold = (int)(float)_bucketCount;
		for ( var bucket = 0; bucket < previousCount; bucket++ )
		{
			var entry = _buckets[ bucket ];
			_buckets[ bucket ] = null;
			while ( entry is not null )
			{
				var next = entry.HashNext;
				var destination = Bucket( entry.Key );
				entry.HashNext = _rehash[ destination ];
				_rehash[ destination ] = entry;
				entry = next;
			}
		}
		(_buckets, _rehash) = (_rehash, _buckets);
	}

	private Entry Find( InputCommandKey key )
	{
		for ( var entry = _buckets[ Bucket( key ) ]; entry is not null; entry = entry.HashNext )
			if ( entry.Key == key )
				return entry;
		return null;
	}
}
