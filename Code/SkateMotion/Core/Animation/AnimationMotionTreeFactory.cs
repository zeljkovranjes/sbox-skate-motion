using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Constructs independent retained motion trees in an owner's bounded storage slots.</summary>
internal sealed class AnimationMotionTreeFactory : IAnimationBasePoseTreeFactory, IAnimationAttributeOverrideConsumer
{
	private readonly RawAnimationClipSet _clips;
	private readonly IReadOnlyDictionary<string, BlendDefinition> _definitions;
	private readonly IReadOnlyList<string> _motions;
	private readonly AnimationBoneTransform[] _reference;
	private readonly IReadOnlyDictionary<AnimationAttributeKey, AnimationAttributeKey> _selectors;
	private readonly Action<IAnimationPosePlaybackNode> _publish;
	private readonly AnimationOwnerPoseContext _ownerContext;
	private readonly AnimationMotionTree[] _slots;
	private AnimationAttributeOverride[] _parameters = Array.Empty<AnimationAttributeOverride>();
	private int _parameterCount;

	internal AnimationMotionTreeFactory( RawAnimationClipSet clips,
		IReadOnlyDictionary<string, BlendDefinition> definitions, IReadOnlyList<string> motions,
		AnimationBoneTransform[] reference,
		IReadOnlyDictionary<AnimationAttributeKey, AnimationAttributeKey> selectors = null,
		Action<IAnimationPosePlaybackNode> publish = null, AnimationOwnerPoseContext ownerContext = null )
		: this( clips, definitions, motions, reference, selectors, publish, ownerContext, 4 )
	{
	}

	internal AnimationMotionTreeFactory( RawAnimationClipSet clips,
		IReadOnlyDictionary<string, BlendDefinition> definitions, IReadOnlyList<string> motions,
		AnimationBoneTransform[] reference,
		IReadOnlyDictionary<AnimationAttributeKey, AnimationAttributeKey> selectors,
		Action<IAnimationPosePlaybackNode> publish, AnimationOwnerPoseContext ownerContext, int storageCount )
	{
		ArgumentNullException.ThrowIfNull( clips );
		ArgumentNullException.ThrowIfNull( definitions );
		ArgumentNullException.ThrowIfNull( motions );
		ArgumentNullException.ThrowIfNull( reference );
		if ( reference.Length != clips.BoneCount )
			throw new ArgumentException( "The owner reference must match the raw clip skeleton.", nameof( reference ) );
		if ( storageCount <= 0 ) throw new ArgumentOutOfRangeException( nameof( storageCount ) );
		_slots = new AnimationMotionTree[ storageCount ];
		_clips = clips;
		_definitions = definitions;
		_motions = motions;
		_reference = reference.AsSpan().ToArray();
		_selectors = selectors;
		_publish = publish;
		_ownerContext = ownerContext;
	}

	internal IAnimationPosePlaybackNode PublishedRoot { get; private set; }

	/// <summary>Replaces one slot with fresh clocks; a missing root returns null and unsupported semantics fail explicitly.</summary>
	public IAnimationPlaybackTree Create( int storageSlot, int definition )
		=> CreateComposed( storageSlot, definition, null, ComposeReference );

	/// <summary>Rates the authored root before owner wrappers and retains its parameters for this storage slot.</summary>
	internal IAnimationPlaybackTree CreateComposed( int storageSlot, int definition, float? rate,
		Func<IAnimationPosePlaybackNode, IAnimationPosePlaybackNode> compose )
	{
		ArgumentNullException.ThrowIfNull( compose );
		if ( (uint)storageSlot >= _slots.Length ) throw new ArgumentOutOfRangeException( nameof( storageSlot ) );
		if ( (uint)definition >= _motions.Count ) throw new ArgumentOutOfRangeException( nameof( definition ) );
		_slots[ storageSlot ] = null;
		var name = _motions[ definition ];
		if ( !_definitions.ContainsKey( name ) && _clips.Find( name ) is null ) return null;
		var identifiers = 0;
		var motion = Build( name, new HashSet<string>( StringComparer.OrdinalIgnoreCase ), ref identifiers );
		if ( rate.HasValue )
		{
			if ( motion.Playback is not IAnimationRatePlaybackNode rated )
				throw new InvalidOperationException( "An authored root must support playback rate control." );
			rated.SetRate( rate.Value );
		}
		var tree = new AnimationMotionTree( motion, compose( motion.Playback ) );
		tree.Update( _parameters.AsMemory( 0, _parameterCount ) );
		_slots[ storageSlot ] = tree;
		return tree;
	}

	/// <summary>Stops parameter updates for exactly the released tree, without clearing a reused slot.</summary>
	internal void Release( int storageSlot, IAnimationPlaybackTree tree )
	{
		if ( (uint)storageSlot >= _slots.Length ) throw new ArgumentOutOfRangeException( nameof( storageSlot ) );
		if ( !ReferenceEquals( _slots[ storageSlot ], tree ) )
			throw new InvalidOperationException( "The released tree does not own this storage slot." );
		_slots[ storageSlot ] = null;
	}

	private IAnimationPosePlaybackNode ComposeReference( IAnimationPosePlaybackNode motion ) =>
		new AnimationCompositionPosePlayback( motion, new AnimationReferencePosePlayback( _reference ), true );

	/// <summary>Publishes the root selected by the separate base owner without advancing or preparing it.</summary>
	public void Publish( IAnimationPosePlaybackNode root )
	{
		PublishedRoot = root;
		_publish?.Invoke( root );
	}

	/// <summary>Preserves ordered key/mode records and updates every retained slot, including outgoing motions.</summary>
	public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes )
	{
		if ( _parameters.Length < attributes.Length ) _parameters = new AnimationAttributeOverride[ attributes.Length ];
		attributes.CopyTo( _parameters );
		_parameterCount = attributes.Length;
		var changed = false;
		foreach ( var tree in _slots )
			if ( tree is not null ) changed |= tree.Update( _parameters.AsMemory( 0, _parameterCount ) );
		return changed;
	}

	private AnimationMotionTreeNode Build( string name, HashSet<string> path, ref int identifiers )
	{
		if ( !path.Add( name ) ) throw new ArgumentException( $"Motion tree contains a cycle at {name}." );
		try
		{
			if ( !_definitions.TryGetValue( name, out var definition ) )
			{
				var clip = _clips.Find( name ) ?? throw new ArgumentException( $"Motion tree is missing {name}." );
				if ( clip.Attributes is null ) throw new ArgumentException( $"Motion {name} needs authored attribute metadata." );
				if ( identifiers > ushort.MaxValue ) throw new ArgumentException( "Motion tree has too many clip leaves." );
				return new( new AnimationClipPlayback( clip, (ushort)identifiers++,
					_ownerContext?.Sampling ?? new AnimationPoseSamplingContext( 1, false ) ) );
			}
			if ( definition.SourceKind is not (7 or 8) )
				throw new NotSupportedException( $"Motion {name} has unsupported authored kind {definition.SourceKind}." );
			if ( string.IsNullOrEmpty( definition.SampleAttribute ) || definition.Children.Count == 0 )
				throw new ArgumentException( $"Motion {name} needs an authored attribute key and children." );
			var key = AnimationAttributeKey.Encode( definition.SampleAttribute );
			if ( definition.SourceKind == 8 )
			{
				var selected = definition.Children[ 0 ];
				if ( _selectors is not null && _selectors.TryGetValue( key, out var value ) )
					foreach ( var child in definition.Children )
						if ( child.Key is not null && AnimationAttributeKey.Encode( child.Key ) == value )
						{
							selected = child;
							break;
						}
				return Build( selected.Name, path, ref identifiers );
			}
			if ( definition.Children.Count < 2 )
				throw new ArgumentException( $"Directional motion {name} needs at least two children." );
			var children = new AnimationMotionTreeNode[ definition.Children.Count ];
			var playback = new IAnimationPosePlaybackNode[ children.Length ];
			for ( var i = 0; i < children.Length; ++i )
			{
				children[ i ] = Build( definition.Children[ i ].Name, path, ref identifiers );
				playback[ i ] = children[ i ].Playback;
			}
			return new( new AnimationDirectionalPosePlayback( playback, new float[ playback.Length ] ),
				children, key, _ownerContext );
		}
		finally
		{
			path.Remove( name );
		}
	}
}
