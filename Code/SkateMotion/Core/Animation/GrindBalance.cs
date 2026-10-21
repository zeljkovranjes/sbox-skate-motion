namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// The timing of a rider's balance on a rail: small corrections of the torso forward and back and from side to side,
/// each a blend of two slow waves (0.47 to 0.93 a second) whose phases only ever run forward, so the sway never repeats
/// exactly and never jumps. It shows only as much as the grind's own clip holds the body still: the clip's torso turn
/// (the spine from the hips up) is watched each frame, and a clip that moves it already gets little or none, while a
/// still pose (a slide held in one key) gets all of it. It eases in once on a rail and out quicker on leaving it.
/// </summary>
internal sealed class GrindBalance
{
	/// <summary>Seconds the sway takes to ease in fully once on a rail, and out again leaving it.</summary>
	internal const float EaseIn = 0.35f;

	internal const float EaseOut = 0.3f;

	/// <summary>
	/// How fast (degrees a second) the clip's own torso may turn and still count as held still (the sway shows in full), and
	/// from how fast it counts as moving (none shows).
	/// </summary>
	internal const float StillBelow = 2f;

	internal const float MovingAbove = 8f;

	/// <summary>Seconds over which the clip's torso speed is averaged before it is judged still or moving.</summary>
	internal const float MotionTime = 0.3f;

	// The waves: paces (a second), the share of each in its axis, and fixed phase offsets so the axes never line up.
	private const float PitchPace = 0.62f;
	private const float PitchNudgePace = 0.93f;
	private const float RollPace = 0.78f;
	private const float RollDriftPace = 0.47f;
	private const float MainShare = 0.65f;

	private float _shown;
	private float _pitch;
	private float _pitchNudge;
	private float _roll;
	private float _rollDrift;
	private Quaternion? _lastTorso;

	/// <summary>The clip's own torso speed, degrees a second, averaged over <see cref="MotionTime"/>.</summary>
	internal float ClipMotion { get; private set; } = MovingAbove;

	/// <summary>How still the clip holds the torso, 0 (moving) to 1 (still).</summary>
	internal float Stillness
	{
		get
		{
			var still = Math.Clamp( (MovingAbove - ClipMotion) / (MovingAbove - StillBelow), 0f, 1f );
			return still * still * (3f - 2f * still);
		}
	}

	/// <summary>How much of the sway shows, 0 to 1: eased onto the rail along a level curve, times the clip's stillness.</summary>
	internal float Weight => _shown * _shown * (3f - 2f * _shown) * Stillness;

	/// <summary>The forward and back lean now, -1 to 1 before the weight.</summary>
	internal float Pitch => Wave( _pitch, _pitchNudge, 1.3f );

	/// <summary>The side to side lean now, -1 to 1 before the weight.</summary>
	internal float Roll => Wave( _roll, _rollDrift, 2.1f );

	/// <summary>
	/// Watches the clip's own torso turn (the spine's rotation from the hips up, as the clip poses it this frame) over
	/// <paramref name="delta"/> seconds, to judge whether the clip holds the body still.
	/// </summary>
	internal void Watch( Quaternion torso, float delta )
	{
		if ( !(delta > 0f) || !float.IsFinite( delta ) || !float.IsFinite( torso.X + torso.Y + torso.Z + torso.W ) )
			return;
		torso = Quaternion.Normalize( torso );
		if ( _lastTorso is { } last )
		{
			var dot = Math.Min( 1f, MathF.Abs( Quaternion.Dot( last, torso ) ) );
			// A burst as a new clip blends in (coming onto a rail the torso turned 290 degrees a second for about 0.3 s)
			// counts no faster than clearly moving: averaged in full, it kept a still slide judged moving for over a
			// second after the blend was done.
			var speed = MathF.Min( 2f * MathF.Acos( dot ) * 180f / MathF.PI / delta, 2f * MovingAbove );
			ClipMotion += (speed - ClipMotion) * (1f - MathF.Exp( -delta / MotionTime ));
		}
		_lastTorso = torso;
	}

	/// <summary>Runs the sway on by <paramref name="delta"/> seconds, easing it in while on a rail and out while not.</summary>
	internal void Update( float delta, bool onRail )
	{
		if ( !(delta > 0f) || !float.IsFinite( delta ) )
			return;
		_shown = Math.Clamp( _shown + (onRail ? delta / EaseIn : -delta / EaseOut), 0f, 1f );
		_pitch = Advance( _pitch, PitchPace, delta );
		_pitchNudge = Advance( _pitchNudge, PitchNudgePace, delta );
		_roll = Advance( _roll, RollPace, delta );
		_rollDrift = Advance( _rollDrift, RollDriftPace, delta );
	}

	private static float Advance( float phase, float pace, float delta )
	{
		phase += pace * delta;
		return phase - MathF.Floor( phase );
	}

	// Two waves mixed, the second offset so the sum never repeats on the first's own beat.
	private static float Wave( float main, float second, float offset ) =>
		MainShare * MathF.Sin( 2f * MathF.PI * main ) + (1f - MainShare) * MathF.Sin( 2f * MathF.PI * second + offset );
}
