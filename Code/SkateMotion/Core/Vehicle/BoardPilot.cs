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

	// Whether push was held at the last step, so a push stroke's start can be told from a held push.
	private bool _pushHeld;

	// Wind-up (seconds) a foot plant pops as hard as: a plant is a quick hop, not a full ollie.
	private const float PlantHold = 0.12f;
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
	private readonly KickTurn _kickTurn = new();
	private readonly HandPlant _plant = new();
	private readonly GrindHold _grindHold = new();
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

	/// <summary>True while the board kick turns on its back wheels (see <see cref="KickTurn"/>).</summary>
	internal bool KickTurning => _kickTurn.Turning;

	/// <summary>True while the rider's hand is planted on a ramp's lip (see <see cref="HandPlant"/>).</summary>
	internal bool HandPlanted => _plant.Planted;

	/// <summary>The lip of the last hand plant (or where the board last left the ground): the surface point it left from.</summary>
	internal Vector3 HandPlantLip => _plant.Lip;

	/// <summary>Level direction out of the wall under <see cref="HandPlantLip"/>.</summary>
	internal Vector3 HandPlantOutward => _plant.Outward;

	// The highest point where a wheel touches the ground, or the deck's middle without wheel contacts. Leaving a wall,
	// that is the wall's top: the lip a hand plants on.
	private static Vector3 SupportPoint( ReadOnlySpan<CollisionContact> contacts, Vector3 fallback )
	{
		var highest = fallback;
		var found = false;
		foreach ( var contact in contacts )
		{
			if ( contact.BodyA < 3 || (found && contact.PointB.Z <= highest.Z) )
				continue;
			highest = contact.PointB;
			found = true;
		}
		return highest;
	}

	/// <summary>Slowing (m/s^2) of a board skidding dark side up on its grip.</summary>
	internal const float DarkSkid = 3f;

	/// <summary>Rolls the board over about its length where it is, keeping its motion: a board held dark side up that the
	/// rider lets go of goes on upside down, as it was shown.</summary>
	internal static void FlipOver( ArticulatedBoard assembly )
	{
		var deck = assembly.Deck;
		var over = Quaternion.Concatenate( deck.Rotation, Quaternion.CreateFromAxisAngle( deck.Forward, MathF.PI ) );
		assembly.Reset( deck.Position, over, deck.Velocity, deck.AngularVelocity );
	}

	/// <summary>Rolls a board lying upside down back over about its length, raised by <paramref name="lift"/> (metres) onto
	/// its wheels and at rest: righted for a rider getting on it.</summary>
	internal static void TurnUpright( ArticulatedBoard assembly, float lift )
	{
		var deck = assembly.Deck;
		if ( deck.Up.Z >= 0f )
			return;
		var over = Quaternion.Concatenate( deck.Rotation, Quaternion.CreateFromAxisAngle( deck.Forward, MathF.PI ) );
		assembly.Reset( deck.Position + Vector3.UnitZ * lift, over, Vector3.Zero, Vector3.Zero );
	}

	// How quickly (seconds) a kick turn's spin follows the steering, and its back wheels' drift is taken away.
	private const float KickTurnResponse = 0.1f;

	// How much faster (radians per second) than its trucks steer it a board on its wheels may turn, and how quickly
	// (seconds) any more is taken off (see CarveHold).
	internal const float CarveSlack = 0.15f;
	private const float CarveHoldResponse = 0.05f;

	// The change of the deck's spin about the ground (world) that takes a turn running past the trucks' own (both trucks'
	// angles on the deck, the wheelbase between them, the speed rolled along the deck) back to CarveSlack past it; zero
	// within it.
	private Vector3 CarveHold( ArticulatedBoard assembly, in BoardBody body, Vector3 normal, float step )
	{
		var forward = body.Forward - normal * Vector3.Dot( body.Forward, normal );
		if ( forward.LengthSquared() < 1e-4f || !(_settings.Wheelbase > 0f) )
			return Vector3.Zero;
		forward = Vector3.Normalize( forward );
		var along = Vector3.Dot( body.Velocity, forward );
		var carve = along * (MathF.Tan( TruckYaw( assembly, 1 ) ) - MathF.Tan( TruckYaw( assembly, 2 ) )) / _settings.Wheelbase;
		var spin = Vector3.Dot( body.AngularVelocity, normal );
		var excess = spin - carve;
		if ( !float.IsFinite( excess ) || MathF.Abs( excess ) <= CarveSlack )
			return Vector3.Zero;
		var target = carve + MathF.CopySign( CarveSlack, excess );
		return normal * ((target - spin) * MathF.Min( 1f, step / CarveHoldResponse ));
	}

	// Cosine of the most the wheels' contact normals may part from their mean for the carve to be held (2 degrees).
	private const float OneSlopeCosine = 0.99939f;

	// Whether every wheel contact's normal lies within OneSlopeCosine of the mean ground normal.
	private static bool OneSlope( ReadOnlySpan<CollisionContact> contacts, Vector3 normal )
	{
		foreach ( var contact in contacts )
		{
			if ( contact.BodyA is < 3 or > 6 )
				continue;
			if ( Vector3.Dot( contact.Normal, normal ) < OneSlopeCosine )
				return false;
		}
		return true;
	}

	// A truck's turn (radians) on the deck about the deck's up, positive to the left.
	private static float TruckYaw( ArticulatedBoard assembly, int truck )
	{
		var relative = Quaternion.Conjugate( assembly.Body( 0 ).Rotation ) * assembly.Body( truck ).Rotation;
		var forward = Vector3.Transform( Vector3.UnitX, relative );
		return MathF.Atan2( forward.Y, forward.X );
	}
	private const float KickTurnHold = 0.03f;
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

	// Seconds after a grind ends that a wheel meeting the rail's side still counts as part of the grind, not a knock: a
	// turned truck's wheel brushed the rail's side the step after an overcrook left its end, and the rider ran out.
	private const float RailExitGrace = 0.25f;

	// Seconds since the board last ground a rail.
	private float _sinceGrind = float.MaxValue;

	// Sideways speed, as a share of the launch speed, that full steering adds at a pop.
	private const float PopSteer = 0.2f;

	/// <param name="landingRail">Direction of the rail the board is coming down on in the air, or null.</param>
	internal PhysicsStep Apply( ArticulatedBoard assembly, ReadOnlySpan<CollisionContact> contacts,
		ControlInput input, float step, GrindLock grind = default, WallSight? wall = null, Vector3? landingRail = null )
	{
		input = input.Sanitized();
		var runout = false;
		if ( _attached && !_bailed
			&& LandingSafety.RequiresBail( assembly, contacts, _settings.BailSpeed, step, out var closing, out var struck,
				grind.Active || landingRail is not null || _sinceGrind < RailExitGrace ) )
		{
			// Measured handling: running into something side-on at a moderate speed while standing upright makes the
			// rider step off and run it out; anything harder, or a crash into the ground, is a bail.
			if ( _settings.Recovered && assembly.Deck.Up.Z > RunoutUpright && MathF.Abs( struck.Z ) < 0.5f
				&& closing < _settings.BailSpeed * RunoutSpeedShare )
				runout = true;
			else
				Bail( "impact" );
		}
		_sinceGrind = grind.Active ? 0f : _sinceGrind + step;
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
			// The deck within reach of a surface its wheels meet this step is the board riding onto it (a bank's face as the
			// front wheels climb it, the nose close above it), not the deck striking something: no collision, no knock.
			if ( contact.BodyA == 0 && Vector3.Dot( contact.PointA - contact.PointB, contact.Normal ) <= 0.004f
				&& !OnWheelSurface( contacts, contact.Normal ) )
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
			_plant.Ground( SupportPoint( contacts, body.Position ), normal );
		}
		if ( _settings.Recovered && _attached && !_bailed && _sinceLanding < LandingAbsorb )
		{
			var rising = Vector3.Dot( body.Velocity, _landingNormal );
			if ( rising > 0f )
				assembly.AddImpulse( 0, -_landingNormal * (rising * _settings.Mass), Vector3.Zero );
		}
		if ( grind.Active )
		{
			// On a rail the deck balances to the grind, even with a wheel brushing the rail's edge: balancing to that
			// edge's slope would tip it further and further over the side.
			normal = GrindControl.Balance( grind, body.Forward, input, GrindControl.LedgeRoll( assembly ) );
			// Measured handling: the board is held over the rail's line, so a little sideways drift left from the
			// lock-on or a bump never slides it off the side. A wheel brushing the rail's edge does not let go of it.
			// On one truck that truck is held there and the board turns about it; in an end slide, the resting end.
			if ( _settings.Recovered && _attached && !_bailed )
			{
				var across = Vector3.Cross( grind.Up, grind.Direction );
				var pin = GrindControl.Pin( assembly, grind.Kind );
				if ( across.LengthSquared() > 0.0001f )
				{
					across = Vector3.Normalize( across );
					var offset = Vector3.Dot( pin - grind.Point, across );
					var drift = Vector3.Dot( body.PointVelocity( pin ), across );
					var pull = -(GrindPin * GrindPin * offset + 2f * GrindPin * drift);
					assembly.AddForce( 0, across * (pull * _settings.Mass), body.Position );
				}
				// The rider stands over the end the board rests on, so its weight does not tip the deck off that end.
				if ( GrindControl.Weight( assembly, grind.Kind, input ) is { } over )
					assembly.AddTorque( 0, Vector3.Cross( body.Position - over, Vector3.UnitZ * (_settings.Mass * _settings.Gravity) ) );
				// On one end the rider's legs take the board's fall onto it: the deck does not seesaw about the rail under its
				// end, which would drop its middle and lift that end off the rail the moment it lands.
				if ( grind.Kind is GrindKind.Noseslide or GrindKind.Tailslide )
				{
					var seesaw = Vector3.Dot( body.AngularVelocity, grind.Direction );
					assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( -grind.Direction * seesaw ) );
				}
				// And turned to the grind's line: along the rail on the trucks (turned off it by steering on one truck),
				// across it in a slide, whichever way round is nearer.
				var line = GrindControl.Line( grind, input );
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
					var change = grind.Up * ((wanted - spin) * Math.Min( 1f, step / GrindTurnResponse ));
					assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( change ) );
					// The board turns about the end it rests on, so turning it does not swing that end off the rail.
					if ( across.LengthSquared() > 0.0001f )
					{
						var swing = Vector3.Dot( Vector3.Cross( change, pin - body.Position ), across );
						for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
							assembly.Body( part ).Velocity -= across * swing;
					}
				}
			}
			// Measured handling: a deck twisted far off its grind balance is a crash.
			if ( !rolling && _settings.Recovered && _attached && !_bailed
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
				// Riding dark side up the rider stands on the board's underside and does not push.
				var push = !rider || _wall.Riding || input.DarkSideUp ? 0f : _settings.Recovered
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
					+ (rider ? input.Brake * _settings.BrakeAcceleration : 0f)
					// Dark side up the board skids on its grip instead of rolling.
					+ (rider && input.DarkSideUp ? DarkSkid : 0f);
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
				// On a rail the trick stick tips the grind (GrindControl.Balance), even with a wheel down on a ledge's top: a
				// manual taken up there pitched a 5-0 on a ledge on past its tip and off the edge.
				if ( rolling && !grind.Active && _manual.TryBalance( assembly, contacts, input, normal, _settings, step, out var manualTorque ) )
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
				// A kick turn: slow with the steering pushed over, the board pivots round on its back wheels.
				var rollingSpeed = count > 0 ? speedSum / count : 0f;
				if ( _settings.Recovered && _kickTurn.Step( rollingSpeed, input.Steer,
					rolling && !grind.Active && balanceMode == MotionMode.Rolling && !_slide.Sliding && !_wall.Riding ) )
				{
					var pivot = assembly.Body( 2 ).Position;
					var spin = Vector3.Dot( body.AngularVelocity, normal );
					var change = normal * ((input.Steer * KickTurn.Rate - spin) * Math.Min( 1f, step / KickTurnResponse ));
					assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( change ) );
					// About the back wheels: the rider stands over them, so they stay where they are while the board swings
					// round them (rolling wheels alone could not hold the swing's pull along the deck).
					var swing = Vector3.Cross( change, pivot - body.Position );
					var drift = assembly.Body( 2 ).Velocity + swing;
					drift = (drift - normal * Vector3.Dot( drift, normal )) * Math.Min( 1f, step / KickTurnHold );
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
						assembly.Body( part ).Velocity -= drift;
				}
				// A carve keeps its line: on its wheels the board turns about the ground no faster than its trucks steer it at the
				// speed it rolls (CarveSlack more at most). At full steer the wheels' sideways grip runs near its limit, and a
				// contact's grip dropping to its sliding share once overcome let a carve's turn run away now and then (-40 to
				// -100 deg/s at 6.9 m/s, still spinning a second after the steering was let go). A board spins out when the
				// rider asks: a powerslide, a revert or a kick turn.
				// Only where every wheel touches ground of one slope: crossing a transition (a floor running into a bank) the
				// wheels' normals disagree and the spin about their mean is partly the board pitching onto the new slope; held
				// back there, subzero's board bounced into the air climbing the lab's bank.
				if ( _settings.Recovered && rolling && !grind.Active && balanceMode == MotionMode.Rolling && !_slide.Sliding
					&& !_kickTurn.Turning && !_wall.Riding && count >= 3 && OneSlope( contacts, normal ) )
				{
					var hold = CarveHold( assembly, body, normal, step );
					if ( hold != Vector3.Zero )
						assembly.AddImpulse( 0, Vector3.Zero, body.AngularImpulse( hold ) );
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
				_kickTurn.Reset();
			}
		}
		else
		{
			_movement.Reset();
			_manual.Reset();
			_slide.Reset();
			_kickTurn.Reset();
			_previousNormal = Vector3.Zero;
			var wallTurn = Vector3.Zero;
			// A hand planted on a steep ramp's lip: the board stalls over it, held up, and drops back in when let go.
			var planted = false;
			if ( _attached && !_bailed && _settings.Recovered )
			{
				planted = _plant.Step( input.HandPlant, body.Position, step, out var released );
				if ( planted )
				{
					// Gravity is added in the step; take it back out so the board hangs still.
					var hold = _plant.HoldVelocity( body.Position ) + Vector3.UnitZ * (_settings.Gravity * step);
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
					{
						assembly.Body( part ).Velocity = hold;
						assembly.Body( part ).AngularVelocity = Vector3.Zero;
					}
				}
				else if ( released )
				{
					assembly.Reset( body.Position, _plant.DropRotation(), _plant.DropVelocity(), Vector3.Zero );
				}
			}
			if ( _attached && !_bailed && _settings.Recovered && !planted )
			{
				var (turn, lift) = _wall.Air( body, wall, step );
				wallTurn = turn;
				if ( lift != Vector3.Zero )
				{
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
						assembly.Body( part ).Velocity += lift;
				}
			}
			if ( planted )
				torque = Vector3.Zero;
			else if ( _wall.Active )
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
					HandlingCurves.AlignRate( _settings.Difficulty ), landingRail ) ) );
			}
			else if ( _attached && !_bailed )
				torque = (body.Up * input.Steer + body.Left * input.Lean) * _settings.AirTorque;
		}
		if ( _attached && !_bailed )
			trick = _tricks.Step( input, supported, step );
		// A foot plant: a push stroke started while holding a grab on the ground (not on a rail) hops the board up, the
		// planted foot springing the rider off the ground (a boneless or fastplant).
		var pushStart = input.Push > 0f && !_pushHeld;
		_pushHeld = input.Push > 0f;
		if ( trick == TrickKind.None && _attached && !_bailed && rolling && !grind.Active && input.Grab && pushStart )
			trick = TrickKind.Plant;
		if ( trick != TrickKind.None )
		{
			// A pop ends a landing's absorb at once.
			_sinceLanding = LandingAbsorb;
			if ( _settings.Recovered )
			{
				var slope = MathF.Acos( Math.Clamp( normal.Z, -1f, 1f ) ) * 180f / MathF.PI;
				var launch = HandlingCurves.PopSpeed( HandlingCurves.PopStrength( trick == TrickKind.Plant ? PlantHold : _tricks.LastHeld ), slope,
					Vector3.Dot( body.Velocity, body.Forward ), _settings.Difficulty );
				// The pop sets the speed off the ground (whatever the board was already doing along the normal, such as
				// rising off a bump, is replaced); steering at the pop carries the board a little sideways.
				var off = Vector3.Dot( body.Velocity, normal );
				impulse = (normal * (launch - off) - body.Left * (PopSteer * launch * input.Steer)) * mass;
			}
			else
				impulse = normal * (mass * _settings.PopSpeed * (0.55f + 0.45f * (trick == TrickKind.Plant ? 0.5f : _tricks.LastCharge)));
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
		var grindKind = grinding && !_bailed ? grind.Kind : GrindKind.None;
		var grindShape = grinding && !_bailed ? GrindShape.Measure( assembly, grind ) : default;
		// The grind as named: decided as it locks on and held through the contacts' step-to-step wobble (see GrindHold).
		var (namedKind, namedShape) = _grindHold.Update( grindKind, grindShape, input, step );
		return new( force, torque, impulse, angularImpulse, System.Numerics.BitOperations.PopCount( wheelMask ),
			_bailed ? MotionMode.Bailed : grinding ? MotionMode.Grinding : supported ? balanceMode : MotionMode.Airborne,
			trick, landed, landed ? impact : 0f )
		{
			Grind = grindKind,
			TrickNose = trick != TrickKind.None && _tricks.Nose,
			GrindShape = grindShape,
			NamedGrind = namedKind,
			NamedGrindShape = namedShape,
			CollisionSpeed = collision,
			Runout = runout,
			PowerSlide = _slide.Side,
			Reverting = _slide.Reverting,
			BodyFlip = _flip.Direction,
			WallRiding = _wall.Riding,
			Crouch = _settings.Recovered ? _measuredPump.Crouch : 0f,
			HandPlanted = _plant.Planted
		};
	}

	// Least cosine between a deck contact's normal and a wheel contact's for the two to be the same surface.
	private const float SameSurface = 0.97f;

	// Whether a wheel touches a surface facing along this normal this step.
	private static bool OnWheelSurface( ReadOnlySpan<CollisionContact> contacts, Vector3 normal )
	{
		foreach ( var contact in contacts )
		{
			if ( contact.BodyA >= 3 && Vector3.Dot( contact.PointA - contact.PointB, contact.Normal ) <= 0.004f
				&& Vector3.Dot( contact.Normal, normal ) > SameSurface )
				return true;
		}
		return false;
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
		_grindHold.Reset();
		_previousNormal = Vector3.Zero;
	}
}
