namespace SkateMotion.Core;

internal sealed class SkateTricks
{
	private float _held;
	private float _origin;
	private float _sweep;
	private float _travel;
	private float _previousX;
	private bool _armed;
	internal float Charge => Math.Clamp( _held / 0.3f, 0f, 1f );
	internal float LastCharge { get; private set; }

	/// <summary>Seconds the last pop was wound up for.</summary>
	internal float LastHeld { get; private set; }
	internal float SpinDirection { get; private set; }
	internal bool Nose { get; private set; }

	internal TrickKind Step( ControlInput input, bool supported, float step )
	{
		if ( !supported ) { Reset(); return TrickKind.None; }
		if ( !_armed && MathF.Abs( input.FlickY ) >= 0.65f )
		{
			_armed = true;
			_origin = MathF.Sign( input.FlickY );
			_held = 0f;
			_sweep = 0f;
			_travel = 0f;
			_previousX = input.FlickX;
		}
		if ( !_armed ) return TrickKind.None;
		_held += step;
		// Spin added by rolling the stick is measured during the windup, before the releasing flick.
		var windupSweep = _sweep;
		var windupTravel = _travel;
		_sweep += input.FlickX - _previousX;
		_travel += MathF.Abs( input.FlickX - _previousX );
		_previousX = input.FlickX;
		var opposite = input.FlickY * _origin < -0.35f;
		var sideways = MathF.Abs( input.FlickX ) > 0.7f && MathF.Abs( input.FlickY ) < 0.5f;
		if ( _held >= 0.025f && (opposite || sideways) )
		{
			LastCharge = Charge;
			LastHeld = _held;
			Nose = _origin > 0f;
			SpinDirection = MathF.Sign( _sweep );
			var side = input.FlickX * (input.Goofy ? -1f : 1f);
			var trick = sideways ? TrickKind.Shuvit : MathF.Abs( side ) > 0.3f
				? (side < 0f ? TrickKind.Kickflip : TrickKind.Heelflip)
				: Nose ? TrickKind.Nollie : TrickKind.Ollie;
			var flips = trick is TrickKind.Kickflip or TrickKind.Heelflip;
			var travel = flips ? windupTravel : _travel;
			var direction = flips && windupSweep != 0f ? MathF.Sign( windupSweep ) : SpinDirection;
			trick = WithSpin( trick, travel, direction * (input.Goofy ? -1f : 1f) > 0f );
			if ( TrickMotion.Spin( trick ) > 0f )
				SpinDirection = direction;
			Reset();
			return trick;
		}
		if ( MathF.Abs( input.FlickY ) < 0.15f && MathF.Abs( input.FlickX ) < 0.15f ) Reset();
		return TrickKind.None;
	}

	// Rolling the stick sideways while flicking adds board spin: more than one stick-width of travel turns a
	// flip into a varial (or hardflip/inward heelflip by direction), more than 2.2 into the 360 version: a 360 flip
	// or 360 hardflip, a laser flip or 360 inward heelflip.
	private static TrickKind WithSpin( TrickKind trick, float sweep, bool frontside )
	{
		var full = sweep > 2.2f;
		var half = sweep > 1.0f;
		return trick switch
		{
			TrickKind.Shuvit => full ? (frontside ? TrickKind.FrontsideShuvit360 : TrickKind.Shuvit360)
				: frontside ? TrickKind.FrontsideShuvit : TrickKind.Shuvit,
			TrickKind.Kickflip when full => frontside ? TrickKind.Hardflip360 : TrickKind.Treflip,
			TrickKind.Kickflip when half => frontside ? TrickKind.Hardflip : TrickKind.VarialKickflip,
			TrickKind.Heelflip when full => frontside ? TrickKind.Laserflip : TrickKind.InwardHeelflip360,
			TrickKind.Heelflip when half => frontside ? TrickKind.VarialHeelflip : TrickKind.InwardHeelflip,
			_ => trick
		};
	}

	internal void Reset()
	{
		_armed = false;
		_held = 0f;
		_sweep = 0f;
		_travel = 0f;
	}
}
