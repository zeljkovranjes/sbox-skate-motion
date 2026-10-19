namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal static class SkateWheels
{
	internal static Vector3 Offset( int index, SkateSettings settings ) => new(
		( index < 2 ? 0.5f : -0.5f ) * settings.Wheelbase,
		( index % 2 == 0 ? 0.5f : -0.5f ) * settings.Track, 0f );

	internal static WheelForce Solve( in BoardBody body, in WheelContact contact,
		int index, ControlInput input, SkateSettings settings, float step, float push, float extension,
		float? suspensionLoad = null )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );

		if ( !contact.Hit || !Finite( contact.Normal ) || !Finite( contact.Point )
			|| !Finite( contact.SurfaceVelocity ) || !float.IsFinite( contact.Distance )
			|| contact.Distance < 0f || contact.Distance > settings.RideHeight + extension
			|| !float.IsFinite( contact.Grip ) || contact.Grip < 0f
			|| !float.IsFinite( contact.Resistance ) || contact.Resistance < 0f
			|| contact.Normal.LengthSquared() < 0.000001f )
			return default;

		var normal = Vector3.Normalize( contact.Normal );
		if ( Vector3.Dot( normal, body.Up ) < 0.15f )
			return default;

		var velocity = body.PointVelocity( contact.Point ) - contact.SurfaceVelocity;
		var normalSpeed = Vector3.Dot( velocity, normal );
		var compression = settings.RideHeight + extension - contact.Distance;
		var quarterMass = settings.Mass * 0.25f;
		// Implicit spring/damper keeps a stiff contact stable over the supported timestep range.
		var load = (settings.Spring * compression - (settings.Damping + settings.Spring * step) * normalSpeed)
			/ (1f + settings.Damping * step / quarterMass + settings.Spring * step * step / quarterMass);
		load = Math.Clamp( load, 0f, settings.Mass * settings.Gravity * 3f );
		load = suspensionLoad ?? load;
		if ( load <= 0f ) return default;

		var speed = velocity.Length();
		var steer = settings.SteeringAngle( input.Steer, speed );
		steer *= index < 2 ? 1f : -1f;
		var direction = body.Forward * MathF.Cos( steer ) + body.Left * MathF.Sin( steer );
		direction -= normal * Vector3.Dot( direction, normal );
		if ( direction.LengthSquared() < 0.000001f )
			return default;

		direction = Vector3.Normalize( direction );
		var lateral = Vector3.Cross( normal, direction );
		var longitudinalSpeed = Vector3.Dot( velocity, direction );
		var lateralSpeed = Vector3.Dot( velocity, lateral );
		var lateralLimit = body.ContactMass( contact.Point, lateral, settings.Mass ) / (4f * step);
		var longitudinalLimit = body.ContactMass( contact.Point, direction, settings.Mass ) / (4f * step);
		var lateralForce = -lateralSpeed * lateralLimit * (input.Slide ? 0.12f : 0.8f);
		var resistance = settings.RollingResistance * contact.Resistance * load
			+ input.Brake * settings.BrakeAcceleration * quarterMass;
		var longitudinalForce = -Math.Clamp( longitudinalSpeed * longitudinalLimit, -resistance, resistance );
		longitudinalForce += push * quarterMass;
		var friction = new Vector3( longitudinalForce, lateralForce, 0f );
		var limit = load * settings.Grip * contact.Grip * (input.Slide ? 0.55f : 1f);
		if ( friction.LengthSquared() > limit * limit ) friction = Vector3.Normalize( friction ) * limit;
		return new( contact.Point, normal * load + direction * friction.X + lateral * friction.Y, load );
	}

	private static bool Finite( Vector3 value ) =>
		float.IsFinite( value.X ) && float.IsFinite( value.Y ) && float.IsFinite( value.Z );
}
