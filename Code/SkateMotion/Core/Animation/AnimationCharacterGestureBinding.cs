using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Owns family-local gesture instances for compiled graph lifecycle dispatch.</summary>
internal sealed class AnimationCharacterGestureBinding
{
	private readonly AnimationCharacterGestureDefinition _definition;
	private readonly IAnimationCharacterGestureContext _context;
	private readonly List<Instance> _instances = new();

	internal AnimationCharacterGestureBinding( AnimationCharacterGestureDefinition definition,
		IAnimationCharacterGestureContext context )
	{
		_definition = definition ?? throw new ArgumentNullException( nameof( definition ) );
		_context = context ?? throw new ArgumentNullException( nameof( context ) );
	}

	internal CompiledStateBehaviorBinding Bind() => new( Create, _ => { }, _ => { }, () => { } );
	internal void Update( int instance ) => Get( instance ).Gesture.Update( _definition, _context );
	internal void Retain( int instance ) => Get( instance ).References++;

	internal void Release( int instance )
	{
		if ( --Get( instance ).References == 0 )
			_instances[ instance ] = null;
	}

	private int Create()
	{
		var instance = new Instance();
		for ( var index = 0; index < _instances.Count; index++ )
		{
			if ( _instances[ index ] is not null )
				continue;
			_instances[ index ] = instance;
			return index;
		}
		_instances.Add( instance );
		return _instances.Count - 1;
	}

	private Instance Get( int instance )
	{
		if ( (uint)instance >= _instances.Count || _instances[ instance ] is null )
			throw new InvalidOperationException( "The gesture instance is not retained." );
		return _instances[ instance ];
	}

	private sealed class Instance
	{
		internal readonly AnimationCharacterGestureInstance Gesture = new();
		internal int References = 1;
	}
}
