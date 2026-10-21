namespace SkateMotion.Core.Animation;

/// <summary>
/// The timing of a rider's breathing. A phase runs on at a pace eased between a resting and a winded rate, so the breath
/// never jumps when the pace changes. Exertion (a push, a landing, a bail) winds the rider over a moment and settles
/// over a few seconds, quickening and deepening the breath meanwhile. A weight eases the whole breath in and out along
/// a smooth curve as it is shown or hidden, so it never starts or stops with a jolt.
/// </summary>
internal sealed class BreathingRhythm
{
	/// <summary>Breaths per second at rest (15 a minute).</summary>
	internal const float RestingRate = 0.25f;

	/// <summary>Breaths per second fully winded (30 a minute).</summary>
	internal const float WindedRate = 0.5f;

	/// <summary>How much deeper a fully winded breath is than a resting one.</summary>
	internal const float WindedDepth = 1.8f;

	/// <summary>Seconds the breath takes to ease in fully once shown.</summary>
	internal const float EaseIn = 1f;

	/// <summary>Seconds the breath takes to ease out fully once hidden: quicker, so it is gone soon after a pop.</summary>
	internal const float EaseOut = 0.35f;

	/// <summary>Seconds the exertion takes to come most of the way (63%) up to a new level.</summary>
	internal const float WindTime = 0.4f;

	/// <summary>Seconds a level of exertion takes to settle to a third (37%) of itself.</summary>
	internal const float SettleTime = 3f;

	// Share of each breath spent breathing in; the rest is spent breathing out.
	private const float InhaleShare = 0.4f;

	// How far the breath has eased in (0 to 1, before the smooth curve), and the exertion level the rider is wound to.
	private float _shown;
	private float _level;

	/// <summary>How far through the current breath (0 to 1): from breathed out, through breathed in, and out again.</summary>
	internal float Phase { get; private set; }

	/// <summary>How winded the rider is, 0 at rest to 1.</summary>
	internal float Exertion { get; private set; }

	/// <summary>Breaths per second now.</summary>
	internal float Rate => RestingRate + (WindedRate - RestingRate) * Exertion;

	/// <summary>Depth of a breath now, 1 at rest.</summary>
	internal float Depth => 1f + (WindedDepth - 1f) * Exertion;

	/// <summary>How much of the breath is shown, 0 to 1, along a curve that starts and ends level.</summary>
	internal float Weight => _shown * _shown * (3f - 2f * _shown);

	/// <summary>How far the chest is breathed in now: 0 breathed out, <see cref="Depth"/> fully in, times the weight.</summary>
	internal float Inhale => Weight * Depth * Fill( Phase );

	/// <summary>Winds the rider to at least <paramref name="amount"/> (0 to 1), taken up over <see cref="WindTime"/>.</summary>
	internal void Exert( float amount )
	{
		if ( float.IsFinite( amount ) )
			_level = Math.Max( _level, Math.Clamp( amount, 0f, 1f ) );
	}

	/// <summary>Runs the breath on by <paramref name="delta"/> seconds, easing it in while shown and out while not.</summary>
	internal void Update( float delta, bool shown )
	{
		if ( !(delta > 0f) || !float.IsFinite( delta ) )
			return;
		_shown = Math.Clamp( _shown + (shown ? delta / EaseIn : -delta / EaseOut), 0f, 1f );
		_level *= MathF.Exp( -delta / SettleTime );
		Exertion += (_level - Exertion) * (1f - MathF.Exp( -delta / WindTime ));
		Phase += Rate * delta;
		Phase -= MathF.Floor( Phase );
	}

	/// <summary>
	/// How full the chest is (0 to 1) at a point of a breath: up along half a cosine over the first part of the breath,
	/// down along the other half over the rest. Both halves are level where they meet, so the breath turns over at the
	/// top and the bottom without a jolt.
	/// </summary>
	internal static float Fill( float phase )
	{
		phase -= MathF.Floor( phase );
		var turn = phase < InhaleShare
			? 0.5f * phase / InhaleShare
			: 0.5f + 0.5f * (phase - InhaleShare) / (1f - InhaleShare);
		return 0.5f - 0.5f * MathF.Cos( 2f * MathF.PI * turn );
	}
}
