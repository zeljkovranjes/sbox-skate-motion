namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal sealed class SkatePhysics
{
	private readonly SkateSettings _settings;
	private readonly SkateMovement _movement = new();
	private readonly SkateTricks _tricks = new();
	private float _contactRelease;
	private bool _supported;
	private bool _bailed;

	internal SkatePhysics( SkateSettings settings )
	{
		ArgumentNullException.ThrowIfNull( settings );
		settings.Validate();
		_settings = settings;
	}

	internal PhysicsStep Step( in BoardBody body, ReadOnlySpan<WheelContact> contacts,
		ControlInput input, float step, Span<WheelForce> wheels )
	{
		if ( !float.IsFinite( step ) || step < 1f / 1000f || step > 1f / 30f + 0.000001f )
			throw new ArgumentOutOfRangeException( nameof( step ), "Use a fixed physics step between 30 and 1000 Hz." );
		if ( contacts.Length != 4 || wheels.Length != 4 )
			throw new ArgumentException( "Exactly four wheel contacts and four force outputs are required." );

		input = input.Sanitized();
		wheels.Clear();
		_contactRelease = Math.Max( 0f, _contactRelease - step );
		var gravity = -Vector3.UnitZ * (_settings.Mass * _settings.Gravity);
		var drag = -body.Velocity * (body.Velocity.Length() * _settings.AirDrag);
		if ( _bailed )
			return new( gravity + drag, default, default, default, 0, MotionMode.Bailed, TrickKind.None, false, 0f );

		var count = 0;
		Span<float> loads = stackalloc float[ 4 ];
		WheelSuspension.Solve( body, contacts, _settings, step, loads );
		var normalSum = Vector3.Zero;
		var relativeSpeed = 0f;
		var impact = 0f;
		for ( var i = 0; i < 4 && _contactRelease <= 0f; i++ )
		{
			var wheel = SkateWheels.Solve( body, contacts[ i ], i, input, _settings, step, 0f, 0f, loads[ i ] );
			wheels[ i ] = wheel;
			if ( wheel.Load <= 0f )
				continue;

			var normal = Vector3.Normalize( contacts[ i ].Normal );
			var relative = body.PointVelocity( wheel.Point ) - contacts[ i ].SurfaceVelocity;
			normalSum += normal;
			relativeSpeed += Vector3.Dot( relative, body.Forward );
			impact = Math.Max( impact, -Vector3.Dot( relative, normal ) );
			count++;
		}

		var supported = count > 0;
		var landed = supported && !_supported;
		var normalTarget = supported ? Vector3.Normalize( normalSum ) : Vector3.UnitZ;
		var push = supported
			? _movement.PushAcceleration( input, relativeSpeed / count, _settings, step ) : 0f;
		if ( !supported )
			_movement.Reset();

		for ( var i = 0; i < 4 && push > 0f; i++ )
			wheels[ i ] = SkateWheels.Solve( body, contacts[ i ], i, input, _settings, step, push, 0f, loads[ i ] );

		var trick = _tricks.Step( input, supported, step );
		var impulse = Vector3.Zero;
		var angularImpulse = Vector3.Zero;
		if ( trick != TrickKind.None )
		{
			var pop = _settings.PopSpeed * (0.55f + 0.45f * _tricks.LastCharge);
			impulse = normalTarget * (_settings.Mass * pop);
			var spin = normalTarget * (_settings.SpinRate * TrickMotion.Spin( trick ) * _tricks.SpinDirection);
			spin += body.Forward * _settings.FlipRate * TrickMotion.Flip( trick ) * (input.Goofy ? -1f : 1f);
			angularImpulse = body.AngularImpulse( spin );
			_contactRelease = 0.1f;
			wheels.Clear();
			count = 0;
			supported = false;
		}

		var torque = supported ? GroundTorque( body, normalTarget, step )
			: body.Up * (input.Steer * _settings.AirTorque)
				+ body.Left * (input.Lean * _settings.AirTorque);
		_supported = supported;
		return new( gravity + drag, torque, impulse, angularImpulse, count,
			supported ? MotionMode.Rolling : MotionMode.Airborne, trick, landed, landed ? impact : 0f );
	}

	internal void Bail()
	{
		_bailed = true;
		_supported = false;
		_tricks.Reset();
		_movement.Reset();
	}

	internal void Reset()
	{
		_bailed = false;
		_supported = false;
		_contactRelease = 0f;
		_tricks.Reset();
		_movement.Reset();
	}

	private Vector3 GroundTorque( in BoardBody body, Vector3 normal, float step )
	{
		// Estimated rider balance assistance, applied as torque rather than pose replacement.
		var tiltError = Vector3.Cross( body.Up, normal );
		// Suspension already damps pitch and roll. A second independently implicit damper
		// double-counts the same velocity and destabilizes a thin deck at 30 Hz.
		var torque = tiltError * _settings.BalanceSpring;
		var local = Vector3.Transform( torque, System.Numerics.Quaternion.Conjugate( body.Rotation ) );
		var compliance = _settings.BalanceDamping * step + _settings.BalanceSpring * step * step;
		local /= Vector3.One + new Vector3( compliance ) / Vector3.Max( body.Inertia, new Vector3( 0.0001f ) );
		return Vector3.Transform( local, body.Rotation );
	}
}
