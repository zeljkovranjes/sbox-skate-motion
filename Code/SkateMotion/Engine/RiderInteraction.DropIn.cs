using Sandbox;

namespace SkateMotion.Engine;

internal sealed partial class RiderInteraction
{
	// Dropping in: the lip and the way down over it, where the feet stop short of the lip, whether the mount control has
	// been let go (the rider then goes over) and when, whether the drop-in was backed out of, and whether the board is down.
	private Vector3 _lip;
	private Vector3 _way;
	private Vector3 _standAt;
	private bool _mountHeld = true;
	private bool _goingOver;
	private float _wentOver;
	private bool _backedOut;
	private bool _backingOut;
	private float _backedAt;

	// How far along its way the playing clip's root travel is (0 to 1; null for a clip that does not travel), and how far
	// the rider has gone along the step up and the going over (never back).
	private float? _travel;
	private float _stepped;
	private float _over;

	// Whether the step up and the going over have been seen played by their clips (their travel came, and went), and
	// whether the step up is done: the animation can run ahead of the interaction's own steps (a long frame), and then
	// the clips, not the time, say how far the drop-in has come.
	private bool _stepSeen;
	private bool _stepDone;
	private bool _overSeen;
	private bool _overEnded;

	// How far through the going over's travel (0 to 1) the board goes down at the latest: before the clip ends.
	private const float OverLatest = 0.9f;

	// How fast (inches per second) the rider goes over the lip, as the clip steps it out onto the board: the board goes
	// down moving at least so, rather than starting slower than the rider and being overtaken by the body on it.
	private float _overSpeed;
	private const float OverSpeedFollow = 20f;

	// Where the playing clip's root is against where its skater stands by the board (inches, in the frame of the stand at
	// the lip; null without one): going over the lip the clip's skater steps sideways onto the board.
	private Vector3? _travelOffset;

	// Farthest (inches) the rider steps sideways going over the lip with the clip.
	private const float SideStepLongest = 16f;

	// Seconds the step back from the lip takes (the clips' step back to a stand).
	private const float StepBack = 1.35f;

	// Seconds the step up to the lip takes; seconds from letting go before the board may go down over the lip, and by
	// when it does (the clip tips it over the lip over 0.45 to 0.7 s, and ends); degrees within which the board shown tipped over
	// the lip lies along the slope to go down; inches short of the lip the feet stop; and how fast (inches per second)
	// the board starts down.
	private const float StepToLip = 1f;
	private const float OverTheLip = 0.4f;
	private const float OverTheLipLongest = 0.62f;
	private const float TippedWithin = 12f;
	private const float ShortOfLip = 10f;
	private const float DropInSpeed = 0.8f * 39.37f;

	// Longest (seconds) the rider waits at the lip with the mount control held.
	private const float DropInLongest = 60f;

	/// <summary>True while a drop-in waits at the lip for the rider to go over or back out.</summary>
	internal bool DropsIn => Action == RiderAction.DropIn;

	/// <summary>True once a drop-in has gone on long enough after the mount control was let go that the board may go down.</summary>
	internal bool DropInDue => DropsIn && _goingOver && !_placed && (_elapsed - _wentOver >= OverTheLip || _overEnded);

	/// <summary>Where the rider stands at the lip, facing over it, while a drop-in has its board to put down; else null.</summary>
	internal Transform? DropInStand => DropsIn && !_placed
		? new Transform( _standAt.WithZ( _startRider.Position.z ), Rotation.LookAt( _way ) ) : null;

	/// <summary>Takes how far along its way the playing clip's root travel is (0 to 1), or null for a clip that does not travel.</summary>
	internal void ObserveTravel( float? progress ) => ObserveTravel( progress, null );

	/// <summary>
	/// Takes how far along its way the playing clip's root travel is (0 to 1), and where its root is against where its
	/// skater stands by the board (inches, in the frame of the stand at the lip); either null without one.
	/// </summary>
	internal void ObserveTravel( float? progress, Vector3? offset )
	{
		_travel = progress;
		_travelOffset = offset;
	}

	/// <summary>Takes whether the mount control is held: letting it go at the lip drops in.</summary>
	internal void ObserveMountHeld( bool held ) => _mountHeld = held;

	/// <summary>True while the rider steps back from the lip, having pulled back at it.</summary>
	internal bool BacksOut => DropsIn && _backingOut;

	/// <summary>Takes the walking control: pulled back at the lip, the rider backs out of the drop-in.</summary>
	internal void ObserveWalking( Vector3 walk )
	{
		if ( DropsIn && !_goingOver && !_backingOut && _elapsed >= StepToLip && Vector3.Dot( walk, _way ) < -BackOut )
		{
			_backingOut = true;
			_backedAt = _elapsed;
		}
	}

	// How far (of the stick's full travel) the walking control is pulled back from the lip to back out.
	private const float BackOut = 0.5f;

	private void BeginDropIn()
	{
		_lip = _motor.EdgeLip;
		_way = _motor.EdgeWay;
		_standAt = _lip - _way * ShortOfLip;
		_mountHeld = true;
		_goingOver = false;
		_backedOut = false;
		_backingOut = false;
		_stepped = 0f;
		_over = 0f;
		_overSpeed = 0f;
		_stepSeen = false;
		_stepDone = false;
		_overSeen = false;
		_overEnded = false;
	}

	private void StepDropIn( float delta )
	{
		if ( _backingOut )
		{
			// Stepping back from the lip to where the rider stood, as the clip steps back; done once it has.
			var back = Ease( Math.Clamp( (_elapsed - _backedAt) / StepBack, 0f, 1f ) );
			var at = _standAt.WithZ( _startRider.Position.z );
			_motor.MoveForInteraction( Vector3.Lerp( at, _startRider.Position, back ), Rotation.LookAt( _way ), delta );
			_backedOut = _elapsed - _backedAt >= StepBack;
			return;
		}
		if ( !_stepDone )
		{
			// Up to the lip as far along as the step's clip has come (its own pace), all the way once it has played, or on
			// time without one.
			if ( _travel is { } step )
			{
				_stepSeen = true;
				_stepped = MathF.Max( _stepped, step );
			}
			else
				_stepped = _stepSeen ? 1f : MathF.Max( _stepped, Ease( Math.Clamp( _elapsed / StepToLip, 0f, 1f ) ) );
			if ( _elapsed >= StepToLip * 1.5f )
				_stepped = 1f;
			var position = Vector3.Lerp( _startRider.Position, _standAt.WithZ( _startRider.Position.z ), _stepped );
			_motor.MoveForInteraction( position, Rotation.Slerp( _startRider.Rotation, Rotation.LookAt( _way ), _stepped ), delta );
			_stepDone = _stepped >= 1f && (_elapsed >= StepToLip || _stepSeen);
			return;
		}
		if ( !_goingOver && !_mountHeld )
		{
			_goingOver = true;
			_wentOver = _elapsed;
		}
		if ( !_goingOver || _placed )
			return;
		// Going over, the rider steps out over the lip toward where the board goes down, as the clip steps onto it, so
		// getting on it there does not move the rider across in one go.
		// As far along as the going over's clip has come, or on time without one.
		var before = _over;
		if ( _travel is { } going )
		{
			_overSeen = true;
			_over = MathF.Max( _over, going );
		}
		else
			_over = _overSeen ? 1f : MathF.Max( _over, Ease( Math.Clamp( (_elapsed - _wentOver) / OverTheLipLongest, 0f, 1f ) ) );
		_overEnded |= _overSeen && _over >= OverLatest;
		var from = _standAt.WithZ( _startRider.Position.z );
		var to = (_lip + _way * RiderMotor.DropInPastLip).WithZ( from.z );
		var stepped = (_over - before) * (to - from).Length / MathF.Max( delta, 1e-4f );
		_overSpeed = MathX.Lerp( _overSpeed, stepped, 1f - MathF.Exp( -OverSpeedFollow * delta ) );
		// Sideways onto the board as the clip's skater steps onto it.
		var side = _travelOffset is { } offset
			? Rotation.LookAt( _way ) * new Vector3( 0f, Math.Clamp( offset.y, -SideStepLongest, SideStepLongest ), 0f ) : Vector3.Zero;
		_motor.MoveForInteraction( Vector3.Lerp( from, to, _over ) + side, Rotation.LookAt( _way ), delta );
	}

	/// <summary>Puts the held board down over the lip and gets on it as the drop-in goes over, from where the board is.</summary>
	internal bool PlaceDropIn( float rideHeight ) => PlaceDropIn( _board.WorldTransform, rideHeight );

	/// <summary>
	/// Puts the held board down over the lip and gets on it as the drop-in goes over: once the board, shown at
	/// <paramref name="shown"/>, has been tipped over the lip to lie along the slope under it (or has gone on long enough).
	/// </summary>
	internal bool PlaceDropIn( Transform shown, float rideHeight )
	{
		if ( !DropInDue )
			return false;
		var under = _motor.SurfaceUnder( shown );
		var tipped = under is { } surface
			&& Vector3.Dot( shown.Rotation.Up, surface.Normal ) >= MathF.Cos( TippedWithin * MathF.PI / 180f );
		if ( !tipped && _elapsed - _wentOver < OverTheLipLongest && !_overEnded )
			return false;
		_placed = _motor.DropIn( _lip, _way, shown, rideHeight, MathF.Max( DropInSpeed, _overSpeed ) );
		// A board that cannot go down there (the ramp has gone) is kept, and the rider stays at the lip.
		_backedOut |= !_placed;
		return _placed;
	}
}
