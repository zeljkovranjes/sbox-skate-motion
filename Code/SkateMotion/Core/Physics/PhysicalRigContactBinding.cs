using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Registers the canonical physical bodies for their owner's contact feedback.</summary>
internal sealed class PhysicalRigContactBinding
{
	private readonly PhysicalSolverWorld _world;
	private readonly PhysicalBodyState[] _bodies = new PhysicalBodyState[ 24 ];
	private bool _released;

	internal PhysicalContactOwner Owner { get; }

	internal PhysicalRigContactBinding( PhysicalSolverWorld world, PhysicalBodyBinding[] bodies,
		PhysicalContactOwner owner )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( owner );
		if ( bodies.Length < _bodies.Length )
			throw new ArgumentException( "Contact binding requires all 24 canonical bodies.", nameof( bodies ) );
		for ( var bone = 0; bone < _bodies.Length; ++bone )
		{
			var body = bodies[ bone ]?.Body;
			if ( world.IndexOfBody( body ) < 0 )
				throw new ArgumentException( "Every canonical contact body must belong to the shared world." );
			for ( var previous = 0; previous < bone; ++previous )
				if ( ReferenceEquals( _bodies[ previous ], body ) )
					throw new ArgumentException( "Canonical contact bodies must have distinct physical identities." );
			_bodies[ bone ] = body;
		}
		_world = world;
		Owner = owner;
		for ( var bone = 0; bone < _bodies.Length; ++bone )
			world.RegisterContactBody( _bodies[ bone ], owner, bone );
	}

	internal void Release()
	{
		if ( _released )
			return;
		foreach ( var body in _bodies )
			_world.UnregisterContactBody( body );
		_released = true;
	}
}
