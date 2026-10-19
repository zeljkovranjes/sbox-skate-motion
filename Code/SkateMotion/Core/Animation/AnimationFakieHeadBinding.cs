using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Dispatches compiled head behaviors while retaining explicit bindings for other families.</summary>
internal sealed class AnimationFakieHeadBinding
{
	private readonly AnimationFakieHeadDefinition?[] _definitions;
	private readonly bool[] _operations;
	private readonly IAnimationFakieHeadContext _context;
	private readonly StateBehaviorCallbacks _external;
	private readonly Action<int, int, int> _externalUpdate;
	private readonly List<Instance> _instances = new();

	internal AnimationFakieHeadBinding( AnimationFakieHeadDefinition?[] definitions, bool[] operations,
		IAnimationFakieHeadContext context, StateBehaviorCallbacks external,
		Action<int, int, int> externalUpdate )
	{
		ArgumentNullException.ThrowIfNull( definitions );
		ArgumentNullException.ThrowIfNull( operations );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( external.Create );
		ArgumentNullException.ThrowIfNull( external.Retain );
		ArgumentNullException.ThrowIfNull( external.Release );
		ArgumentNullException.ThrowIfNull( external.Enter );
		ArgumentNullException.ThrowIfNull( external.Exit );
		ArgumentNullException.ThrowIfNull( external.Transition );
		ArgumentNullException.ThrowIfNull( externalUpdate );
		_definitions = definitions.AsSpan().ToArray();
		_operations = operations.AsSpan().ToArray();
		_context = context;
		_external = external;
		_externalUpdate = externalUpdate;
		Callbacks = new( Create, Retain, Release, Enter, Exit, Transition );
	}

	internal StateBehaviorCallbacks Callbacks { get; }

	internal CompiledStateBehaviorBinding Bind( int definition )
	{
		if ( (uint)definition >= _definitions.Length )
			throw new ArgumentOutOfRangeException( nameof( definition ) );
		return new( () => Create( definition ), instance => Enter( definition, instance ),
			instance => Exit( definition, instance ), () => Transition( definition ) );
	}

	internal void Update( int operation, int definition, int instance )
	{
		if ( !_operations[ operation ] )
		{
			_externalUpdate( operation, definition, ExternalHandle( instance ) );
			return;
		}
		var arguments = _definitions[ definition ]
			?? throw new InvalidOperationException( "A head update requires its channel and motion identities." );
		Head( instance ).Update( arguments, _context );
	}

	private int Create( int definition )
	{
		Instance value;
		if ( _definitions[ definition ].HasValue )
			value = new() { Head = new() };
		else
		{
			var handle = _external.Create( definition );
			if ( handle == -1 )
				return -1;
			if ( handle < -1 )
				throw new InvalidOperationException( "An external factory returned an invalid instance." );
			value = new() { ExternalHandle = handle };
		}
		for ( var index = 0; index < _instances.Count; index++ )
		{
			if ( _instances[ index ] is not null )
				continue;
			_instances[ index ] = value;
			return index;
		}
		_instances.Add( value );
		return _instances.Count - 1;
	}

	private void Retain( int instance )
	{
		var value = Retained( instance );
		if ( value.Head is null )
			_external.Retain( value.ExternalHandle );
		value.References++;
	}

	private void Release( int instance )
	{
		var value = Retained( instance );
		if ( value.Head is null )
			_external.Release( value.ExternalHandle );
		if ( --value.References == 0 )
			_instances[ instance ] = null;
	}

	private void Enter( int definition, int instance )
	{
		if ( _definitions[ definition ].HasValue )
			Head( instance ).Enter();
		else
			_external.Enter( definition, ExternalHandle( instance ) );
	}

	private void Exit( int definition, int instance )
	{
		if ( _definitions[ definition ].HasValue )
			Head( instance ).Exit();
		else
			_external.Exit( definition, ExternalHandle( instance ) );
	}

	private void Transition( int definition )
	{
		if ( !_definitions[ definition ].HasValue )
			_external.Transition( definition );
	}

	private Instance Retained( int instance )
	{
		if ( (uint)instance >= _instances.Count || _instances[ instance ] is null )
			throw new InvalidOperationException( "The behavior instance is not retained." );
		return _instances[ instance ];
	}

	private AnimationFakieHeadInstance Head( int instance ) => Retained( instance ).Head
		?? throw new InvalidOperationException( "The selected instance is not a head behavior." );

	private int ExternalHandle( int instance )
	{
		if ( instance == -1 )
			return -1;
		var value = Retained( instance );
		if ( value.Head is not null )
			throw new InvalidOperationException( "An external operation selected a head instance." );
		return value.ExternalHandle;
	}

	private sealed class Instance
	{
		internal int References = 1;
		internal int ExternalHandle = -1;
		internal AnimationFakieHeadInstance Head;
	}
}
