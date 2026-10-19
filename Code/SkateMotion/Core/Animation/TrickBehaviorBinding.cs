using System.Collections.Generic;
using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Binds retained trick consumers to graph lifecycle and compiled update operations.</summary>
internal sealed class TrickBehaviorBinding
{
	private readonly TrickBehaviorKind[] _definitions;
	private readonly TrickBehaviorKind[] _operations;
	private readonly InputCommandKey[] _continuationKeys;
	private readonly InputCommandKey _catchKey;
	private readonly InputRequestBuffer _requests;
	private readonly Func<AnimationAttributeQueryResult[]> _attributes;
	private readonly Func<int, TrickHeightSettings> _heightSettings;
	private readonly Action<int, TrickHeightResult> _publishHeight;
	private readonly StateBehaviorCallbacks _external;
	private readonly Action<int, int, int> _externalUpdate;
	private readonly List<TrickBehaviorInstance> _instances = new();

	internal uint Flags { get; set; }
	internal int CurrentInstance { get; set; } = -1;
	internal StateBehaviorCallbacks Callbacks { get; }

	internal TrickBehaviorBinding( TrickBehaviorKind[] definitions, TrickBehaviorKind[] operations,
		InputCommandKey[] continuationKeys, InputCommandKey catchKey, InputRequestBuffer requests,
		Func<AnimationAttributeQueryResult[]> attributes, Func<int, TrickHeightSettings> heightSettings,
		Action<int, TrickHeightResult> publishHeight, StateBehaviorCallbacks external,
		Action<int, int, int> externalUpdate )
	{
		ArgumentNullException.ThrowIfNull( definitions );
		ArgumentNullException.ThrowIfNull( operations );
		ArgumentNullException.ThrowIfNull( continuationKeys );
		ArgumentNullException.ThrowIfNull( requests );
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( heightSettings );
		ArgumentNullException.ThrowIfNull( publishHeight );
		ArgumentNullException.ThrowIfNull( external.Create );
		ArgumentNullException.ThrowIfNull( external.Retain );
		ArgumentNullException.ThrowIfNull( external.Release );
		ArgumentNullException.ThrowIfNull( external.Enter );
		ArgumentNullException.ThrowIfNull( external.Exit );
		ArgumentNullException.ThrowIfNull( external.Transition );
		ArgumentNullException.ThrowIfNull( externalUpdate );
		foreach ( var kind in definitions )
			ValidateKind( kind );
		foreach ( var kind in operations )
			ValidateKind( kind );
		_definitions = definitions.AsSpan().ToArray();
		_operations = operations.AsSpan().ToArray();
		_continuationKeys = continuationKeys.AsSpan().ToArray();
		_catchKey = catchKey;
		_requests = requests;
		_attributes = attributes;
		_heightSettings = heightSettings;
		_publishHeight = publishHeight;
		_external = external;
		_externalUpdate = externalUpdate;
		Callbacks = new( Create, Retain, Release, Enter, Exit, Transition );
	}

	internal void Update( int operation, int definition, int instance )
	{
		CurrentInstance = instance;
		switch ( _operations[ operation ] )
		{
			case TrickBehaviorKind.Continuation:
				var monitor = Monitor( instance );
				monitor.Flags = Flags;
				monitor.Update( _requests );
				Flags = monitor.Flags;
				break;
			case TrickBehaviorKind.External:
				_externalUpdate( operation, definition, ExternalHandle( instance ) );
				break;
		}
	}

	private int Create( int definition )
	{
		if ( _definitions[ definition ] == TrickBehaviorKind.Height )
			return -1;
		TrickBehaviorInstance value;
		if ( _definitions[ definition ] == TrickBehaviorKind.Continuation )
			value = new() { Monitor = new( _continuationKeys, _catchKey ) };
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
		var value = Instance( instance );
		if ( value.Monitor is null )
			_external.Retain( value.ExternalHandle );
		value.References++;
	}

	private void Release( int instance )
	{
		var value = Instance( instance );
		if ( value.Monitor is null )
			_external.Release( value.ExternalHandle );
		if ( --value.References == 0 )
			_instances[ instance ] = null;
	}

	private void Enter( int definition, int instance )
	{
		switch ( _definitions[ definition ] )
		{
			case TrickBehaviorKind.Continuation:
				Monitor( instance ).Enter();
				break;
			case TrickBehaviorKind.Height:
				var result = TrickHeightController.Evaluate( _requests, _attributes(), _heightSettings( definition ) );
				_publishHeight( definition, result );
				break;
			default:
				_external.Enter( definition, ExternalHandle( instance ) );
				break;
		}
	}

	private void Exit( int definition, int instance )
	{
		if ( _definitions[ definition ] == TrickBehaviorKind.Continuation )
		{
			var monitor = Monitor( instance );
			monitor.Flags = Flags;
			monitor.Exit();
			Flags = monitor.Flags;
		}
		else if ( _definitions[ definition ] == TrickBehaviorKind.External )
			_external.Exit( definition, ExternalHandle( instance ) );
	}

	private void Transition( int definition )
	{
		if ( _definitions[ definition ] == TrickBehaviorKind.External )
			_external.Transition( definition );
		else
			Enter( definition, CurrentInstance );
	}

	private TrickBehaviorInstance Instance( int instance )
	{
		if ( instance < 0 || instance >= _instances.Count || _instances[ instance ] is null )
			throw new InvalidOperationException( "The behavior instance is not retained." );
		return _instances[ instance ];
	}

	private TrickContinuationMonitor Monitor( int instance ) => Instance( instance ).Monitor
		?? throw new InvalidOperationException( "The selected behavior instance is not a continuation monitor." );

	private int ExternalHandle( int instance )
	{
		if ( instance == -1 )
			return -1;
		var value = Instance( instance );
		if ( value.Monitor is not null )
			throw new InvalidOperationException( "An external callback selected a continuation instance." );
		return value.ExternalHandle;
	}

	private static void ValidateKind( TrickBehaviorKind kind )
	{
		if ( kind is not (TrickBehaviorKind.External or TrickBehaviorKind.Continuation or TrickBehaviorKind.Height) )
			throw new ArgumentOutOfRangeException( nameof( kind ) );
	}
}
