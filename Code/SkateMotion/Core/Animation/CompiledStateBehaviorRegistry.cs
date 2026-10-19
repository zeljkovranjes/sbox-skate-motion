using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects independent behavior families to one compiled graph and retained instance space.</summary>
internal sealed class CompiledStateBehaviorRegistry
{
	private readonly CompiledStateBehaviorBinding[] _behaviors;
	private readonly Action<int, int>[] _operations;
	private readonly List<Instance> _instances = new();
	private readonly Stack<int> _freeInstances = new();
	private readonly Dictionary<(CompiledBehaviorInstanceLifetime Lifetime, int Local), int> _handles = new();
	private bool _built;

	internal CompiledStateBehaviorRegistry( int behaviors, int operations )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( behaviors );
		ArgumentOutOfRangeException.ThrowIfNegative( operations );
		_behaviors = new CompiledStateBehaviorBinding[ behaviors ];
		_operations = new Action<int, int>[ operations ];
	}

	// Reuse the same lifetime object for every definition sharing a local instance space.
	// Omitting it is valid only for factories that return the null-instance handle.
	internal void RegisterBehavior( int definition, CompiledStateBehaviorBinding binding,
		CompiledBehaviorInstanceLifetime lifetime = null )
	{
		EnsureMutable();
		if ( (uint)definition >= _behaviors.Length )
			throw new ArgumentOutOfRangeException( nameof( definition ) );
		if ( _behaviors[ definition ].Create is not null )
			throw new InvalidOperationException( "The behavior is already registered." );
		ArgumentNullException.ThrowIfNull( binding.Create );
		ArgumentNullException.ThrowIfNull( binding.Enter );
		ArgumentNullException.ThrowIfNull( binding.Exit );
		ArgumentNullException.ThrowIfNull( binding.Transition );
		_behaviors[ definition ] = new( () => Create( binding, lifetime ),
			instance => binding.Enter( Local( instance, lifetime ) ),
			instance => binding.Exit( Local( instance, lifetime ) ), binding.Transition );
	}

	internal void RegisterOperation( int operation, Action<int, int> update,
		CompiledBehaviorInstanceLifetime lifetime = null )
	{
		EnsureMutable();
		if ( (uint)operation >= _operations.Length )
			throw new ArgumentOutOfRangeException( nameof( operation ) );
		if ( _operations[ operation ] is not null )
			throw new InvalidOperationException( "The operation is already registered." );
		ArgumentNullException.ThrowIfNull( update );
		_operations[ operation ] = ( definition, instance ) => update( definition, Local( instance, lifetime ) );
	}

	internal CompiledStateGraphRuntime Build( CompiledStateGraphDefinition definition,
		Func<int, bool, bool>[] guards, Action invalidateConditions, bool callbacksEnabled )
	{
		EnsureMutable();
		// Runtime validation rejects every missing behavior and referenced operation/guard.
		var runtime = new CompiledStateGraphRuntime( definition, _behaviors, Retain, Release,
			guards, _operations, invalidateConditions, callbacksEnabled );
		_built = true;
		return runtime;
	}

	private int Create( CompiledStateBehaviorBinding binding, CompiledBehaviorInstanceLifetime lifetime )
	{
		var local = binding.Create();
		if ( local == -1 )
			return -1;
		if ( local < -1 || lifetime is null )
			throw new InvalidOperationException( "A retained factory needs an explicit instance lifetime." );
		var key = (lifetime, local);
		if ( _handles.TryGetValue( key, out var existing ) )
		{
			// The factory already transferred its reference; do not retain it twice.
			_instances[ existing ].References++;
			return existing;
		}
		var value = new Instance { Lifetime = lifetime, Local = local, References = 1 };
		var handle = _instances.Count;
		if ( _freeInstances.Count > 0 )
		{
			handle = _freeInstances.Pop();
			_instances[ handle ] = value;
		}
		else
		{
			_instances.Add( value );
		}
		_handles.Add( key, handle );
		return handle;
	}

	private void Retain( int handle )
	{
		var value = Retained( handle );
		value.Lifetime.Retain( value.Local );
		value.References++;
	}

	private void Release( int handle )
	{
		var value = Retained( handle );
		value.Lifetime.Release( value.Local );
		if ( --value.References != 0 )
			return;
		_handles.Remove( (value.Lifetime, value.Local) );
		_instances[ handle ] = null;
		_freeInstances.Push( handle );
	}

	private int Local( int handle, CompiledBehaviorInstanceLifetime lifetime )
	{
		if ( handle == -1 )
			return -1;
		var value = Retained( handle );
		if ( lifetime is not null && !ReferenceEquals( lifetime, value.Lifetime ) )
			throw new InvalidOperationException( "The selected instance belongs to another behavior family." );
		return value.Local;
	}

	private Instance Retained( int handle )
	{
		if ( (uint)handle >= _instances.Count || _instances[ handle ] is null )
			throw new InvalidOperationException( "The behavior instance is not retained." );
		return _instances[ handle ];
	}

	private void EnsureMutable()
	{
		if ( _built )
			throw new InvalidOperationException( "A behavior registry belongs to one immutable runtime." );
	}

	private sealed class Instance
	{
		internal CompiledBehaviorInstanceLifetime Lifetime;
		internal int Local;
		internal int References;
	}
}
