using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

internal sealed partial class PhysicalSolverWorld
{
	internal void RegisterContactBody( PhysicalBodyState body, PhysicalContactOwner owner, int bone )
	{
		var index = ContactBodyIndex( body );
		ContactCollector.Register( body, owner, bone );
		body.MotionMode |= 8;
		if ( _prepared )
			_bodies[ index ].MotionFlags = body.MotionMode;
	}

	internal void UnregisterContactBody( PhysicalBodyState body )
	{
		var index = ContactBodyIndex( body );
		// Removal restores the base mode even if the body has no retained registration.
		body.MotionMode &= 7;
		if ( _prepared )
			_bodies[ index ].MotionFlags = body.MotionMode;
		ContactCollector.Unregister( body );
	}

	private int ContactBodyIndex( PhysicalBodyState body )
	{
		ArgumentNullException.ThrowIfNull( body );
		var index = IndexOfBody( body );
		if ( index < 0 )
			throw new ArgumentException( "A registered contact body must belong to this world.", nameof( body ) );
		return index;
	}
}
