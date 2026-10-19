using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects retained playback instances and actions to compiled graph dispatch and one base owner.</summary>
internal sealed class AnimationPlayBehaviorBinding : IAnimationPlayBehaviorContext
{
	private readonly AnimationPlayBehaviorDefinition?[] _definitions;
	private readonly bool[] _playOperations;
	private readonly IReadOnlyDictionary<int, AnimationEntryAction[]> _actions;
	private readonly AnimationBasePoseTreeOwner _owner;
	private readonly int _fallbackMotion;
	private readonly IAnimationEntryActionContext _actionContext;
	private readonly IAnimationPlayActorContext _actorContext;
	private readonly StateBehaviorCallbacks _external;
	private readonly Action<int, int, int> _externalUpdate;
	private readonly List<Instance> _instances = new();

	internal AnimationPlayBehaviorBinding( AnimationPlayBehaviorDefinition?[] definitions,
		bool[] playOperations, IReadOnlyDictionary<int, AnimationEntryAction[]> actions,
		AnimationBasePoseTreeOwner owner, int fallbackMotion,
		IAnimationEntryActionContext actionContext, IAnimationPlayActorContext actorContext,
		StateBehaviorCallbacks external, Action<int, int, int> externalUpdate )
	{
		ArgumentNullException.ThrowIfNull( definitions );
		ArgumentNullException.ThrowIfNull( playOperations );
		ArgumentNullException.ThrowIfNull( actions );
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( actionContext );
		ArgumentNullException.ThrowIfNull( actorContext );
		ArgumentNullException.ThrowIfNull( external.Create );
		ArgumentNullException.ThrowIfNull( external.Retain );
		ArgumentNullException.ThrowIfNull( external.Release );
		ArgumentNullException.ThrowIfNull( external.Enter );
		ArgumentNullException.ThrowIfNull( external.Exit );
		ArgumentNullException.ThrowIfNull( external.Transition );
		ArgumentNullException.ThrowIfNull( externalUpdate );
		foreach ( var definition in definitions )
		{
			if ( definition is { } play && (!actions.TryGetValue( play.EntryAction, out var list ) || list is null) )
				throw new ArgumentException( "Every playback definition needs an explicit action list, including empty lists.", nameof( actions ) );
		}
		_definitions = definitions.AsSpan().ToArray();
		_playOperations = playOperations.AsSpan().ToArray();
		_actions = actions;
		_owner = owner;
		_fallbackMotion = fallbackMotion;
		_actionContext = actionContext;
		_actorContext = actorContext;
		_external = external;
		_externalUpdate = externalUpdate;
		Callbacks = new( Create, Retain, Release, Enter, Exit, Transition );
	}

	internal StateBehaviorCallbacks Callbacks { get; }
	internal int CurrentInstance { get; set; } = -1;

	internal CompiledStateBehaviorBinding Bind( int definition )
	{
		if ( (uint)definition >= _definitions.Length )
			throw new ArgumentOutOfRangeException( nameof( definition ) );
		return new( () => Create( definition ), instance => Enter( definition, instance ),
			instance => Exit( definition, instance ), () => Transition( definition ) );
	}

	internal void Update( int operation, int definition, int instance )
	{
		CurrentInstance = instance;
		if ( _playOperations[ operation ] )
		{
			var play = _definitions[ definition ]
				?? throw new InvalidOperationException( "A playback operation needs playback definition arguments." );
			Playback( instance ).Update( _actions[ play.EntryAction ], _actionContext );
		}
		else
		{
			_externalUpdate( operation, definition, ExternalHandle( instance ) );
		}
	}

	private int Create( int definition )
	{
		Instance value;
		if ( _definitions[ definition ].HasValue )
			value = new() { Playback = new() };
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
		if ( value.Playback is null )
			_external.Retain( value.ExternalHandle );
		value.References++;
	}

	private void Release( int instance )
	{
		var value = Retained( instance );
		if ( value.Playback is null )
			_external.Release( value.ExternalHandle );
		if ( --value.References == 0 )
			_instances[ instance ] = null;
	}

	private void Enter( int definition, int instance )
	{
		if ( _definitions[ definition ] is { } play )
			AnimationPlayBehavior.Enter( play, instance, _fallbackMotion, this );
		else
			_external.Enter( definition, ExternalHandle( instance ) );
	}

	private void Exit( int definition, int instance )
	{
		if ( _definitions[ definition ].HasValue )
			Playback( instance ).Exit();
		else
			_external.Exit( definition, ExternalHandle( instance ) );
	}

	private void Transition( int definition )
	{
		if ( _definitions[ definition ].HasValue )
			Enter( definition, CurrentInstance );
		else
			_external.Transition( definition );
	}

	private Instance Retained( int instance )
	{
		if ( (uint)instance >= _instances.Count || _instances[ instance ] is null )
			throw new InvalidOperationException( "The behavior instance is not retained." );
		return _instances[ instance ];
	}

	private AnimationPlayBehaviorInstance Playback( int instance ) => Retained( instance ).Playback
		?? throw new InvalidOperationException( "The selected instance does not contain playback state." );

	private int ExternalHandle( int instance )
	{
		if ( instance == -1 )
			return -1;
		var value = Retained( instance );
		if ( value.Playback is not null )
			throw new InvalidOperationException( "An external operation selected a playback instance." );
		return value.ExternalHandle;
	}

	void IAnimationPlayBehaviorContext.MarkEntered( int instance ) => Playback( instance ).Enter();
	void IAnimationPlayBehaviorContext.ExecuteEntryAction( int action, int instance ) =>
		AnimationEntryActionProcessor.Execute( _actions[ action ], true, _actionContext );
	void IAnimationPlayBehaviorContext.RefreshCharacterSelection() => _actorContext.RefreshCharacterSelection();
	AnimationPlayTransition IAnimationPlayBehaviorContext.ReadTransitionOverride() => _actorContext.ReadTransitionOverride();
	void IAnimationPlayBehaviorContext.ConsumeTransitionOverride() => _actorContext.ConsumeTransitionOverride();
	bool IAnimationPlayBehaviorContext.IsAlternateStance() => _actorContext.IsAlternateStance();
	bool IAnimationPlayBehaviorContext.IsMirrored() => _actorContext.IsMirrored();
	bool IAnimationPlayBehaviorContext.UseOffBoardVariant() => _actorContext.UseOffBoardVariant();
	void IAnimationPlayBehaviorContext.SetActorMode( byte mode ) => _actorContext.SetActorMode( mode );
	bool IAnimationPlayBehaviorContext.TryReplace( AnimationBasePoseRequest request ) => _owner.Replace( request );

	private sealed class Instance
	{
		internal int References = 1;
		internal int ExternalHandle = -1;
		internal AnimationPlayBehaviorInstance Playback;
	}
}
