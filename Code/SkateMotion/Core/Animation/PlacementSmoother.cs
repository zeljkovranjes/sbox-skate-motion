namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Keeps a body's shown placement continuous where what it is placed from jumps. Getting on or off the board changes it
/// from the rider's own position to the deck's, and the two seldom agree (getting on from the ground the body would
/// jump up by the deck's height); on foot the rider's body can be stepped up or down at once (a kerb, a stair, the
/// deck's edge). Each jump goes into an offset that a critically damped spring brings back to zero, so the body carries
/// on from where it was shown, at the speed it was moving, and settles onto its new placement.
/// </summary>
internal sealed class PlacementSmoother
{
	private Vector3 _offset;
	private Vector3 _offsetRate;
	private Vector3 _target;
	private Vector3 _shown;
	private Vector3 _shownRate;
	private float _rise;
	private bool _ready;
	private bool _source;
	private bool _stepping;

	/// <summary>How fast the offset dies away (per second): nineteen parts in twenty gone in a quarter second.</summary>
	internal const float Settle = 20f;

	/// <summary>Inches beyond which a jump is the body being placed somewhere else (a respawn), shown at once.</summary>
	internal const float Reach = 48f;

	/// <summary>
	/// Inches one frame's move up or down may grow beyond the frame before's: more is a step taken at once (a slope is
	/// walked onto over a frame or two, a kerb is not).
	/// </summary>
	internal const float StepHeight = 0.5f;

	/// <summary>
	/// Falling speed (inches per second) a landing on foot is taken at once up to: the body stops with the feet. Past it a
	/// hard landing gives at the knees: <see cref="LandingGive"/> of the excess carries the body on down, and the offset
	/// brings it back up, so the body does not stop dead from a long fall in one frame.
	/// </summary>
	internal const float HardLanding = 200f;
	internal const float LandingGive = 0.5f;

	/// <summary>What is still added to the target placement.</summary>
	internal Vector3 Offset => _offset;

	/// <summary>Forgets the shown placement, so the next one is shown where it is.</summary>
	internal void Reset() => _ready = false;

	/// <summary>
	/// The placement to show for <paramref name="target"/>. <paramref name="source"/> says what it is placed from (true on
	/// the deck): a change of it carries the shown placement on. With <paramref name="stepping"/> (standing on foot) a
	/// sudden move along <paramref name="up"/> is a step taken at once and is eased; with <paramref name="keepHeight"/>
	/// (getting on, whose clip stands on the ground the board stands on) the body keeps its height whatever the target
	/// does along <paramref name="up"/>, and lets it go when that ends.
	/// </summary>
	internal Vector3 Update( Vector3 target, bool source, bool stepping, bool keepHeight, Vector3 up, float delta )
	{
		var step = target - _target;
		if ( !_ready || !(delta > 0f) || !Finite( target ) || step.Length() > Reach )
		{
			_ready = Finite( target );
			_offset = Vector3.Zero;
			_offsetRate = Vector3.Zero;
			_target = target;
			_shown = target;
			_shownRate = Vector3.Zero;
			_rise = 0f;
			_source = source;
			_stepping = stepping;
			return target;
		}
		var rise = Vector3.Dot( step, up );
		// Across a step taken at once, the next frame's move up or down is measured against the frame before the step, so a
		// step spread over two frames (a stair the walker climbs in two physics steps) is taken as one rather than its second
		// half going through (the body rose 1.2 in in a frame between frames of 0.3 and 0.6).
		var stepped = false;
		// Landing on foot from a fall (in the air the frame before or not): what it was falling faster than HardLanding
		// partly carries on as a give at the knees.
		if ( stepping && !keepHeight && source == _source )
		{
			var falling = -_rise / delta;
			if ( falling > HardLanding && rise > _rise * 0.5f )
				_offsetRate -= up * ((falling - HardLanding) * LandingGive);
		}
		if ( source != _source )
		{
			Absorb( _shown + _shownRate * delta - (target + _offset) );
		}
		else if ( keepHeight )
		{
			Absorb( -up * rise );
			rise = 0f;
		}
		else if ( stepping && _stepping )
		{
			// Only what grows beyond the frame before's rise or fall is the step: a landing (a fall stopping) never is.
			var most = MathF.Abs( _rise ) + StepHeight;
			if ( MathF.Abs( rise ) > most )
			{
				var excess = rise - MathF.CopySign( most, rise );
				Absorb( -up * excess );
				rise -= excess;
				// Steepening ground (a ramp walked onto) grows its allowance frame by frame; a stair does not.
				stepped = MathF.Abs( excess ) > StepHeight;
			}
		}
		// x(t) = (x0 + (v0 + w x0) t) e^(-w t), and its rate, over this frame; held along up while the height is kept.
		var held = Vector3.Dot( _offset, up );
		var fall = MathF.Exp( -Settle * delta );
		var drive = _offsetRate + _offset * Settle;
		_offset = (_offset + drive * delta) * fall;
		_offsetRate = (_offsetRate - drive * (Settle * delta)) * fall;
		if ( keepHeight )
		{
			_offset += up * (held - Vector3.Dot( _offset, up ));
			_offsetRate -= up * Vector3.Dot( _offsetRate, up );
		}
		var shown = target + _offset;
		_shownRate = (shown - _shown) / delta;
		_shown = shown;
		_target = target;
		_rise = stepped ? _rise : rise;
		_source = source;
		_stepping = stepping;
		return shown;
	}

	private void Absorb( Vector3 jump )
	{
		if ( Finite( jump ) && jump.Length() <= Reach )
			_offset += jump;
	}

	private static bool Finite( Vector3 value ) => float.IsFinite( value.X ) && float.IsFinite( value.Y ) && float.IsFinite( value.Z );
}
