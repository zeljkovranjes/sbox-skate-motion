using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

// Estimated rider actions on the rigid-body assembly.
internal sealed class BoardPilot
{
	private readonly SkateSettings _settings;
	private readonly SkateMovement _movement = new();
	private readonly SkateTricks _tricks = new();
	private readonly ManualBalance _manual = new();
	private readonly PumpStroke _pump = new();
	private Vector3 _previousNormal;
	private bool _supported;
	private bool _bailed;
	private bool _attached = true;

	internal BoardPilot( SkateSettings settings ) => _settings = settings;

	/// <summary>
	/// True while an animation shows the board's tricks (flips and spins relative to the rider): the pop then adds
	/// no flip or spin of its own and the deck keeps only the rotation physics gives it, so the board the animation
	/// shows and the physical board agree again when the trick is caught.
	/// </summary>
	internal bool AnimatedTricks { get; set; }

	/// <summary>True while a rider stands on the board (not bailed).</summary>
	internal bool Ridden => _attached && !_bailed;

	private readonly AirControl _air = new();
	private readonly PowerSlide _slide = new();
	private readonly BodyFlip _flip = new();
	private readonly PumpControl _measuredPump = new();
	private readonly WallRide _wall = new();

	/// <summary>True while the board rides along a wall.</summary>
	internal bool WallRiding => _wall.Riding;

	// How fast (rad/s) the ground's normal turns under the travel, positive where the ground curves up ahead.
	private float GroundTurn( Vector3 normal, Vector3 velocity, float step )
	{
		if ( !_supported || _previousNormal == Vector3.Zero || step <= 0f )
			return 0f;
		var across = Vector3.Cross( velocity, normal );
		if ( across.LengthSquared() < 0.0001f )
			return 0f;
		return Vector3.Dot( Vector3.Cross( _previousNormal, normal ) / step, Vector3.Normalize( across ) );
	}
	private float _takeoffHeight;

	/// <summary>True while a powerslide or revert has the wheels sliding.</summary>
	internal bool PowerSliding => _slide.Sliding;
	private float _sinceLanding = float.PositiveInfinity;
	private Vector3 _landingNormal = Vector3.UnitZ;

	// How stiffly (per second) a grinding board is held over the rail's line (critically damped), how fast it turns
	// toward the grind's line per radian off it, and how quickly (seconds) its spin follows.
	private const float GrindPin = 12f;
	private const float GrindTurn = 8f;
	private const float GrindTurnResponse = 0.05f;

	// Seconds after a landing during which the board is held from springing back up, and seconds off the ground
	// that make a flight (shorter is a wheel lifting at a lip).
	private const float LandingAbsorb = 0.25f;
	private const float FlightTime = 0.1f;
	private float _airTime;

	// Runout instead of a bail: deck at least this upright (up axis height), and approach below this share of the
	// bail speed.
	private const float RunoutUpright = 0.81f;
	private const float RunoutSpeedShare = 1.5f;

	// Sideways speed, as a share of the launch speed, that full steering adds at a pop.
	private const float PopSteer = 0.2f;

	internal PhysicsStep Apply( ArticulatedBoard assembly, ReadOnlySpan<CollisionContact> contacts,
		ControlInput input, float step, GrindLock grind = default, WallSight? wall = null )
	{
		input = input.Sanitized();
		var runout = false;
		if ( _attached && !_bailed
			&& LandingSafety.RequiresBail( assembly, contacts, _settings.BailSpeed, step, out var closing, out var struck ) )
		{
			// Measured handling: running into something side-on at a moderate speed while standing upright makes the
			// rider step off and run it out; anything harder, or a crash into the ground, is a bail.
			if ( _settings.Recovered && assembly.Deck.Up.Z > RunoutUpright && MathF.Abs( struck.Z ) < 0.5f
				&& closing < _settings.BailSpeed * RunoutSpeedShare )
				runout = true;
			else
				Bail( "impact" );
		}
		if ( _attached && _bailed )
		{
			_attached = false;
			assembly.SetRiderAttached( false );
		}
		var body = assembly.Deck;
		var normalSum = Vector3.Zero;
		var speedSum = 0f;
		var impact = 0f;
		var count = 0;
		var wheelMask = 0u;
		var collision = 0f;
		foreach ( var contact in contacts )
		{
			if ( contact.BodyA == 0 && Vector3.Dot( contact.PointA - contact.PointB, contact.Normal ) <= 0.004f )
				collision = Math.Max( collision, -Vector3.Dot( body.PointVelocity( contact.PointA )
					- SurfaceVelocity( assembly, contact ), contact.Normal ) );
			if ( contact.BodyA < 3 || Vector3.Dot( contact.PointA - contact.PointB, contact.Normal ) > 0.004f )
				continue;
			ref var surface = ref assembly.Body( contact.BodyB );
			var surfaceVelocity = surface.Velocity + Vector3.Cross( surface.AngularVelocity, contact.PointB - surface.Position );
			var relative = body.PointVelocity( contact.PointA ) - surfaceVelocity;
			normalSum += contact.Normal;
			speedSum += Vector3.Dot( relative, body.Forward );
			impact = Math.Max( impact, -Vector3.Dot( relative, contact.Normal ) );
			count++;
			wheelMask |= 1u << (contact.BodyA - 3);
		}
		var rolling = count > 0;
		var supported = rolling || grind.Active;
		var landed = supported && !_supported;
		if ( supported )
			_air.Land();

		var normal = normalSum.LengthSquared() > 0.000001f ? Vector3.Normalize( normalSum ) : Vector3.UnitZ;
		if ( supported )
			_landingNormal = normal;
		// Measured handling: the rider's legs take a landing, so for a moment after touching down the board does not
		// spring back up off the ground.
		// Only a real flight counts: a wheel lifting for a moment at a ramp's lip is not a landing to absorb.
		if ( landed && _airTime >= FlightTime )
			_sinceLanding = 0f;
		else
			_sinceLanding += step;
		_airTime = supported ? 0f : _airTime + step;
		if ( supported )
		{
			_takeoffHeight = body.Position.Z;
			_flip.Reset();
		}
		if ( _settings.Recovered && _attached && !_bailed && _sinceLanding < LandingAbsorb )
		{
			var rising = Vector3.Dot( body.Velocity, _landingNormal );
			if ( rising > 0f )
				assembly.AddImpulse( 0, -_landingNormal * (rising * _settings.Mass), Vector3.Zero );
		}
		if ( !rolling && grind.Active )
		{
			normal = GrindBalance.Target( grind, input );
			// Measured handling: the board is held over the rail's line, so a little sideways drift left from the
			// lock-on or a bump never slides it off the side.
			if ( _settings.Recovered && _attached && !_bailed )
			{
				var across = Vector3.Cross( grind.Up, grind.Direction );
				if ( across.LengthSquared() > 0.0001f )
				{
					across = Vector3.Normalize( across );
					var offset = Vector3.Dot( body.Position - grind.Point, across );
					var drift = Vector3.Dot( body.Velocity, across );
					var pull = -(GrindPin * GrindPin * offset + 2f * GrindPin * drift);
					assembly.AddForce( 0, across * (pull * _settings.Mass), body.Position );
				}
				// And turned to the grind's line: along the rail on the trucks, across it in a boardslide, whichever
				// way round is nearer.
				var line = grind.Kind == GrindKind.Boardslide ? Vector3.Cross( grind.Up, grind.Direction ) : grind.Direction;
				var heading = body.Forward - grind.Up * Vector3.Dot( body.Forward, grind.Up );
				if ( heading.LengthSquared() > 0.0001f && line.LengthSquared() > 0.0001f )
				{
					heading = Vector3.Normalize( heading );
					line = Vector3.Normalize( line );
					if ( Vector3.Dot( heading, line ) < 0f )
						line = -line;
					var turn = MathF.Asin( Math.Clamp( Vector3.Dot( Vector3.Cross( heading, line ), grind.Up ), -1f, 1f ) );
					var spin = Vector3.Dot( body.AngularVelocity, grind.Up );
					var wanted = turn * GrindTurn;
					assembly.AddImpulse( 0, Vector3.Zero,
						body.AngularImpulse( grind.Up * ((wanted - spin) * Math.Min( 1f, step / GrindTurnResponse )) ) );
				}
			}
			// Measured handling: a deck twisted far off its grind balance is a crash.
			if ( _settings.Recovered && _attached && !_bailed
				&& MathF.Acos( Math.Clamp( Vector3.Dot( body.Up, normal ), -1f, 1f ) ) > HandlingCurves.GrindDeckLimit )
				Bail( "grind balance" );
		}
		var mass = _attached ? _settings.Mass : _settings.BoardMass;
		var force = -body.Velocity * (body.Velocity.Length() * _settings.AirDrag);
		var torque = Vector3.Zero;
		var impulse = Vector3.Zero;
		var angularImpulse = Vector3.Zero;
		var trick = TrickKind.None;
		var balanceMode = MotionMode.Rolling;
		if ( supported )
		{
			var rider = _attached && !_bailed;
			if ( rolling )
			{
				var speed = speedSum / count;
				var forward = body.Forward - normal * Vector3.Dot( body.Forward, normal );
				if ( forward.LengthSquared() > 0.0001f )
					forward = Vector3.Normalize( forward );
				// Measured handling: riding a wall, the board is held to it and kept flat to it, and nobody pushes.
				if ( _settings.Recovered && rider )
				{
					var (hold, keep, away) = _wall.OnSurface( body, normal, step );
					if ( hold != Vector3.Zero )
					{
						assembly.AddForce( 0, hold * _settings.Mass, body.Position );
						assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( keep ) );
					}
					if ( away != Vector3.Zero )
					{
						for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
							assembly.Body( part ).Velocity += away;
					}
				}
				// Measured handling: riding backwards, a push drives the way the board is rolling.
				var backwards = _settings.Recovered && speed < -0.3f;
				var push = !rider || _wall.Riding ? 0f : _settings.Recovered
					? _movement.RecoveredAcceleration( input, MathF.Abs( speed ), _settings, step ) * (backwards ? -1f : 1f)
					: _movement.PushAcceleration( input, speed, _settings, step );
				var curvature = _supported && _previousNormal != Vector3.Zero
					? MathF.Acos( Math.Clamp( Vector3.Dot( _previousNormal, normal ), -1f, 1f ) )
						/ Math.Max( MathF.Abs( speed ) * step, 0.0001f )
					: 0f;
				// Measured handling pumps from the rider rising on ground that turns up under the board.
				var pump = !rider ? 0f : _settings.Recovered
					? _measuredPump.Acceleration( input.Pump, MathF.Abs( speed ), GroundTurn( normal, body.Velocity, step ), step,
						MathF.Acos( Math.Clamp( normal.Z, -1f, 1f ) ),
						_settings.AutoPump ? HandlingCurves.UnheldPump( _settings.Difficulty ) : 0f )
					: _pump.Acceleration( input.Pump, MathF.Abs( speed ), curvature, step );
				push += pump * MathF.Sign( speed );
				_previousNormal = normal;
				// Measured handling: the wheels' rolling resistance at riding speeds, more when very fast.
				var deceleration = (_settings.Recovered
						? MathF.Max( HandlingCurves.RollingDeceleration( speed ), _settings.RollingResistance * _settings.Gravity )
						: _settings.RollingResistance * _settings.Gravity)
					+ (rider ? input.Brake * _settings.BrakeAcceleration : 0f);
				force += forward * (mass * (push - Math.Clamp( speed / step, -deceleration, deceleration )));
			}
			else
			{
				_movement.Reset();
			}
			if ( rider )
			{
				var tilt = Vector3.Cross( body.Up, normal ) * _settings.BalanceSpring;
				var damping = (body.AngularVelocity - normal * Vector3.Dot( body.AngularVelocity, normal ))
					* _settings.BalanceDamping;
				var local = Vector3.Transform( tilt - damping, Quaternion.Conjugate( body.Rotation ) );
				var compliance = _settings.BalanceDamping * step + _settings.BalanceSpring * step * step;
				torque = Vector3.Transform( local / (Vector3.One + new Vector3( compliance ) / body.Inertia), body.Rotation );
				if ( rolling && _manual.TryBalance( assembly, contacts, input, normal, _settings, step, out var manualTorque ) )
				{
					torque = manualTorque;
					balanceMode = _manual.Mode;
					if ( _settings.Recovered && balanceMode != MotionMode.Rolling )
					{
						// A manual's own slowing stands in for the rolling resistance already applied above.
						var along = Vector3.Dot( body.Velocity, body.Forward );
						var extra = MathF.Max( 0f, HandlingCurves.ManualDeceleration( along ) - _settings.RollingResistance * _settings.Gravity );
						var slowing = Math.Min( extra, MathF.Abs( along ) / step );
						force -= body.Forward * (MathF.Sign( along ) * mass * slowing);
					}
				}
				// Powerslides and reverts turn the board across or round its travel.
				if ( _settings.Recovered )
				{
					var slideTurn = _slide.Step( body, normal, input.Steer, input.Slide, true, step );
					if ( slideTurn != Vector3.Zero )
						assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( slideTurn ) );
				}
				// A landing tilted further from the ground than its speed allows is a crash.
				if ( landed && _settings.Recovered )
				{
					var landingTilt = MathF.Acos( Math.Clamp( Vector3.Dot( body.Up, normal ), -1f, 1f ) );
					var landingTravel = body.Velocity - normal * Vector3.Dot( body.Velocity, normal );
					if ( landingTilt > HandlingCurves.MaxLandingAngle( landingTravel.Length() ) )
						Bail( "landing tilt" );
				}
			}
			else
			{
				// Grinding or sliding on the deck: no powerslide carries on underneath.
				_slide.Reset();
			}
		}
		else
		{
			_movement.Reset();
			_manual.Reset();
			_slide.Reset();
			_previousNormal = Vector3.Zero;
			var wallTurn = Vector3.Zero;
			if ( _attached && !_bailed && _settings.Recovered )
			{
				var (turn, lift) = _wall.Air( body, wall, step );
				wallTurn = turn;
				if ( lift != Vector3.Zero )
				{
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
						assembly.Body( part ).Velocity += lift;
				}
			}
			if ( _wall.Active )
				assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( wallTurn ) );
			else if ( _attached && !_bailed && _settings.Recovered && _airTime >= FlightTime )
			{
				// Grabbing and leaning flips rider and board over together; leaning alone tips the board.
				var flight = BodyFlip.FlightLeft( body.Position.Z - _takeoffHeight, body.Velocity.Z, _settings.Gravity );
				var flip = _flip.Step( body, input.Lean, input.Grab, input.Goofy, flight, step );
				if ( _flip.Flipping )
					assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( flip ) );
				else
					torque = body.Left * (input.Lean * _settings.AirTorque);
				assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( _air.Step( body, input.Steer, _settings.Gravity, step,
					HandlingCurves.AlignRate( _settings.Difficulty ) ) ) );
			}
			else if ( _attached && !_bailed )
				torque = (body.Up * input.Steer + body.Left * input.Lean) * _settings.AirTorque;
		}
		if ( _attached && !_bailed )
			trick = _tricks.Step( input, supported, step );
		if ( trick != TrickKind.None )
		{
			// A pop ends a landing's absorb at once.
			_sinceLanding = LandingAbsorb;
			if ( _settings.Recovered )
			{
				var slope = MathF.Acos( Math.Clamp( normal.Z, -1f, 1f ) ) * 180f / MathF.PI;
				var launch = HandlingCurves.PopSpeed( HandlingCurves.PopStrength( _tricks.LastHeld ), slope,
					Vector3.Dot( body.Velocity, body.Forward ), _settings.Difficulty );
				// The pop sets the speed off the ground (whatever the board was already doing along the normal, such as
				// rising off a bump, is replaced); steering at the pop carries the board a little sideways.
				var off = Vector3.Dot( body.Velocity, normal );
				impulse = (normal * (launch - off) - body.Left * (PopSteer * launch * input.Steer)) * mass;
			}
			else
				impulse = normal * (mass * _settings.PopSpeed * (0.55f + 0.45f * _tricks.LastCharge));
			if ( !AnimatedTricks )
			{
				var spin = normal * (_settings.SpinRate * TrickMotion.Spin( trick ) * _tricks.SpinDirection);
				spin += body.Forward * (_settings.FlipRate * TrickMotion.Flip( trick ) * (input.Goofy ? -1f : 1f));
				angularImpulse = body.AngularImpulse( spin );
			}
			assembly.AddImpulse( 0, impulse, angularImpulse );
			supported = false;
			count = 0;
			wheelMask = 0;
		}
		assembly.AddForce( 0, force, body.Position );
		assembly.AddTorque( 0, torque );
		_supported = supported;
		var grinding = grind.Active && supported && balanceMode == MotionMode.Rolling;
		return new( force, torque, impulse, angularImpulse, System.Numerics.BitOperations.PopCount( wheelMask ),
			_bailed ? MotionMode.Bailed : grinding ? MotionMode.Grinding : supported ? balanceMode : MotionMode.Airborne,
			trick, landed, landed ? impact : 0f )
		{
			Grind = grinding && !_bailed ? grind.Kind : GrindKind.None,
			CollisionSpeed = collision,
			Runout = runout,
			PowerSlide = _slide.Side,
			Reverting = _slide.Reverting,
			BodyFlip = _flip.Direction,
			WallRiding = _wall.Riding,
			Crouch = _settings.Recovered ? _measuredPump.Crouch : 0f
		};
	}

	private static Vector3 SurfaceVelocity( ArticulatedBoard assembly, in CollisionContact contact )
	{
		ref var surface = ref assembly.Body( contact.BodyB );
		return surface.Velocity + Vector3.Cross( surface.AngularVelocity, contact.PointB - surface.Position );
	}

	internal void SetRiderAttached( bool attached )
	{
		var keepBail = _bailed && !attached;
		_attached = attached;
		Reset();
		_bailed = keepBail;
	}

	internal void Bail( string reason = "requested" )
	{
		Reset();
		_bailed = true;
		BailReason = reason;
	}

	/// <summary>Why the board last bailed: "impact", "landing tilt", "grind balance" or "requested".</summary>
	internal string BailReason { get; private set; }

	internal void Reset()
	{
		_bailed = false;
		_supported = false;
		_movement.Reset();
		_tricks.Reset();
		_manual.Reset();
		_pump.Reset();
		_measuredPump.Reset();
		_slide.Reset();
		_flip.Reset();
		_wall.Reset();
		_previousNormal = Vector3.Zero;
	}
}
