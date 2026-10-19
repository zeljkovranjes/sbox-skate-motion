using Sandbox;
using System.Collections.Generic;

namespace SkateMotion.Engine;

internal sealed class BodyConfiguration
{
	private readonly Rigidbody _body;
	private readonly bool _gravity;
	private readonly bool _ccd;
	private readonly bool _events;
	private readonly bool _motion;
	private readonly float _mass;
	private readonly float _linear;
	private readonly float _angular;
	private readonly List<(PhysicsShape Shape, bool Solid)> _shapes = new();

	internal BodyConfiguration( Rigidbody body )
	{
		_body = body;
		_gravity = body.Gravity;
		_ccd = body.EnhancedCcd;
		_events = body.CollisionEventsEnabled;
		_motion = body.MotionEnabled;
		_mass = body.MassOverride;
		_linear = body.LinearDamping;
		_angular = body.AngularDamping;
	}

	internal void Apply( float mass )
	{
		if ( !_body.IsValid() )
			return;
		_body.MassOverride = mass;
		_body.Gravity = false;
		_body.LinearDamping = 0f;
		_body.AngularDamping = 0f;
		_body.EnhancedCcd = true;
		_body.CollisionEventsEnabled = true;
		foreach ( var shape in _body.PhysicsBody.Shapes )
		{
			var saved = false;
			foreach ( var entry in _shapes )
				saved |= entry.Shape == shape;
			if ( !saved ) _shapes.Add( (shape, shape.EnableSolidCollisions) );
			// The board solver already applies both sides of each contact.
			// Keep this shape queryable without a second, infinite-mass native response.
			shape.EnableSolidCollisions = false;
		}
	}

	internal void Restore()
	{
		if ( !_body.IsValid() )
			return;
		_body.MassOverride = _mass;
		_body.Gravity = _gravity;
		_body.LinearDamping = _linear;
		_body.AngularDamping = _angular;
		_body.EnhancedCcd = _ccd;
		_body.CollisionEventsEnabled = _events;
		_body.MotionEnabled = _motion;
		foreach ( var entry in _shapes )
			if ( entry.Shape.IsValid() ) entry.Shape.EnableSolidCollisions = entry.Solid;
	}
}
