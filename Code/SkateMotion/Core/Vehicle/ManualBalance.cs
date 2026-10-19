using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

// Estimated rider weight transfer and balance; no pose or velocity replacement.
internal sealed class ManualBalance
{
	private float _blend;
	private float _held;
	private float _time;

	// Seconds the stick is held before a manual starts; stick past which it is a pop instead; target pitch (radians)
	// at that stick; wobble size (radians) and rate (per second) at speed.
	private const float EngageTime = 0.2f;
	private const float FullStick = 0.9f;
	private const float FullPitch = 0.419f;
	private const float Wobble = 0.05f;
	private const float WobbleRate = 1.5f;

	// Measured handling pitch: a quarter of full pitch at the lightest touch rising with the stick, with a slow wobble
	// that grows with speed (m/s).
	private float RecoveredPitch( float amount, float speed )
	{
		var pitch = FullPitch * (0.25f + 0.75f * amount);
		var shake = 0.29f + 0.71f * Math.Clamp( speed / 7.85f, 0f, 1f );
		return pitch + MathF.Sin( 2f * MathF.PI * WobbleRate * _time ) * Wobble * shake;
	}
	private int _direction;
	internal MotionMode Mode { get; private set; } = MotionMode.Rolling;

	internal bool TryBalance( ArticulatedBoard assembly, ReadOnlySpan<CollisionContact> contacts,
		ControlInput input, Vector3 normal, SkateSettings settings, float step, out Vector3 torque )
	{
		torque = Vector3.Zero;
		Mode = MotionMode.Rolling;
		var amount = Math.Abs( input.FlickY );
		// Measured handling: a full stick is a pop's windup, never a manual.
		var most = settings.Recovered ? FullStick : 0.6f;
		if ( amount < 0.2f || amount > most || Math.Abs( input.FlickX ) > 0.25f )
		{
			Reset();
			return false;
		}
		// Measured handling: the stick has to be held a moment before the rider shifts onto one axle.
		_held += step;
		if ( settings.Recovered && _held < EngageTime )
			return false;
		var direction = Math.Sign( input.FlickY );
		var front = 0;
		var rear = 0;
		var pivot = Vector3.Zero;
		foreach ( var contact in contacts )
		{
			if ( contact.BodyA < 3 || Vector3.Dot( contact.PointA - contact.PointB, contact.Normal ) > 0.004f )
				continue;
			var nose = contact.BodyA < 5;
			if ( nose ) front++;
			else rear++;
			if ( nose == (direction > 0) )
				pivot += contact.PointA;
		}
		var supports = direction > 0 ? front : rear;
		if ( supports == 0 )
		{
			Reset();
			return false;
		}
		if ( _direction != direction )
			_blend = 0f;
		_direction = direction;
		_blend = Math.Min( 1f, _blend + step * 6f );
		var body = assembly.Deck;
		_time += step;
		var angle = direction * (settings.Recovered ? RecoveredPitch( amount, body.Velocity.Length() ) : 0.06f + (amount - 0.2f) * 0.3f) * _blend;
		var target = Vector3.Transform( normal, Quaternion.CreateFromAxisAngle( body.Left, angle ) );
		var error = Vector3.Transform( Vector3.Cross( body.Up, target ), Quaternion.Conjugate( body.Rotation ) );
		var angular = body.AngularVelocity - normal * Vector3.Dot( body.AngularVelocity, normal );
		var rate = Vector3.Transform( angular, Quaternion.Conjugate( body.Rotation ) );
		var spring = new Vector3( settings.BalanceSpring, 2000f, settings.BalanceSpring );
		var damping = new Vector3( settings.BalanceDamping, 160f, settings.BalanceDamping );
		var response = (error * spring - rate * damping)
			/ (Vector3.One + (damping * step + spring * (step * step)) / body.Inertia);
		// Moving the rider's weight toward the supporting axle supplies a gravity
		// moment. The remaining feedback balances pitch and roll through body torque.
		var supportArm = pivot / supports - body.Position;
		var weightMoment = Vector3.Cross( supportArm,
			-Vector3.UnitZ * (settings.Mass * settings.Gravity) ) * _blend;
		torque = weightMoment + Vector3.Transform( response, body.Rotation )
			+ HeadingTorque( body, normal, supportArm, input.Steer, settings, step ) * _blend;
		var raised = -direction * Vector3.Dot( body.Forward, normal ) > 0.03f;
		if ( raised && (direction > 0 ? rear == 0 : front == 0) )
			Mode = direction > 0 ? MotionMode.NoseManual : MotionMode.Manual;
		return true;
	}

	private static Vector3 HeadingTorque( BoardBody body, Vector3 normal, Vector3 supportArm, float steer,
		SkateSettings settings, float step )
	{
		var forward = body.Forward - normal * Vector3.Dot( body.Forward, normal );
		var velocity = body.Velocity - normal * Vector3.Dot( body.Velocity, normal );
		if ( forward.LengthSquared() < 0.0001f )
			return Vector3.Zero;
		forward = Vector3.Normalize( forward );
		var speed = Vector3.Dot( velocity, forward );
		var alignment = velocity.LengthSquared() > 0.04f
			? Vector3.Dot( Vector3.Cross( forward, Vector3.Normalize( velocity ) * Math.Sign( speed ) ), normal ) : 0f;
		var steering = settings.Recovered
			? settings.TruckAngle * HandlingCurves.SteerResponse( steer ) * HandlingCurves.SteerAtSpeed( speed ) * SteeringResponse.ManualShare
			: settings.SteeringAngle( steer, speed );
		var targetRate = 2f * speed * MathF.Tan( steering ) / settings.Wheelbase;
		var inertia = Vector3.Dot( normal, body.AngularImpulse( normal ) );
		// A single leading axle can amplify yaw with speed. Scale the estimated
		// rider correction with transported load and its lever arm, rather than
		// allowing a small initial yaw error to become a jackknife.
		var damping = 4f + 2f * settings.Mass * Math.Abs( speed * Vector3.Dot( supportArm, forward ) ) / inertia;
		var acceleration = (alignment * 8f + (targetRate - Vector3.Dot( body.AngularVelocity, normal )) * damping)
			/ (1f + damping * step);
		return body.AngularImpulse( normal * Math.Clamp( acceleration, -20f, 20f ) );
	}

	internal void Reset()
	{
		_blend = 0f;
		_direction = 0;
		Mode = MotionMode.Rolling;
		_held = 0f;
		_time = 0f;
	}
}
