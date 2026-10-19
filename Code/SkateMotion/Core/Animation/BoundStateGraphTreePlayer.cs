using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Binds caller parameters to retained trees without inferring references or coordinate modes.</summary>
internal sealed class BoundStateGraphTreePlayer
{
	private readonly BaseAnimationPlaybackDefinition _binding;
	private readonly AnimationMotionTreeFactory _factory;
	private readonly AnimationOwnerPoseContext _ownerContext;
	private readonly AnimationAttributeKey[] _keys;
	private readonly AnimationAttributeOverride[] _records;
	private readonly Dictionary<AnimationAttributeKey, AnimationAttributeKey> _selectors = new();

	internal BoundStateGraphTreePlayer( ClipSet clips, AnimationGraphDefinition graph,
		AnimationOwnerPoseContext ownerContext = null )
	{
		_ownerContext = ownerContext;
		_binding = graph.BasePlayback ?? throw new ArgumentException( "Base playback needs explicit owner bindings." );
		var raw = clips.RawPoses ?? throw new ArgumentException( "Base playback needs raw clips and attributes." );
		if ( (uint)_binding.ReferenceIndex >= raw.ReferenceCount )
			throw new ArgumentException( "The owner reference index must select an available reference pose." );
		if ( string.IsNullOrEmpty( _binding.PhaseParameter ) )
			throw new ArgumentException( "Base playback needs an explicit locomotion phase parameter." );
		var names = new List<string>();
		foreach ( var clip in clips.Clips )
			names.Add( clip.Name );
		foreach ( var pair in _binding.ConstructionSelectors )
			_selectors.Add( AnimationAttributeKey.Encode( pair.Key ), AnimationAttributeKey.Encode( pair.Value ) );
		_factory = new AnimationMotionTreeFactory( raw, graph.Blends, names,
			raw.Reference( _binding.ReferenceIndex ).ToArray(), _selectors, ownerContext: ownerContext );
		Player = new StateGraphTreePlayer( graph.StateMachine, _factory, names );
		_keys = new AnimationAttributeKey[ _binding.Parameters.Count ];
		_records = new AnimationAttributeOverride[ _keys.Length ];
		for ( var i = 0; i < _keys.Length; i++ )
		{
			var binding = _binding.Parameters[ i ];
			if ( string.IsNullOrEmpty( binding.Parameter ) || string.IsNullOrEmpty( binding.Attribute ) )
				throw new ArgumentException( "Scalar bindings need both parameter and attribute names." );
			_keys[ i ] = AnimationAttributeKey.Encode( binding.Attribute );
		}
	}

	internal StateGraphTreePlayer Player { get; }

	/// <summary>Changes future construction only; existing trees retain their selected children.</summary>
	internal void SetConstructionSelector( AnimationAttributeKey key, AnimationAttributeKey value ) => _selectors[ key ] = value;

	internal bool RemoveConstructionSelector( AnimationAttributeKey key ) => _selectors.Remove( key );

	internal bool Update( StateFacts facts, AnimationParameters parameters, float delta, SkeletonPose output )
	{
		if ( !parameters.TryGetNumber( _binding.PhaseParameter, out var phase ) )
			throw new InvalidOperationException( "The locomotion phase producer has not supplied its bound parameter." );
		var count = 0;
		for ( var i = 0; i < _keys.Length; i++ )
		{
			var binding = _binding.Parameters[ i ];
			if ( !parameters.TryGetNumber( binding.Parameter, out var value ) )
				continue;
			var payload = new AnimationAttributePayload( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 );
			var attribute = new AnimationAttributeQueryResult( payload, _keys[ i ], -1f, -1f, 2, 0 );
			_records[ count++ ] = new( attribute, -1, binding.Normalized );
		}
		_factory.UpdateOverrides( _records.AsSpan( 0, count ) );
		return Player.Update( facts, delta, phase, output,
			_ownerContext?.ConsumeHistory ?? true, _ownerContext?.SamplingFlags ?? 1 );
	}
}
