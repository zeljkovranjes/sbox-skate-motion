namespace SkateMotion.Core.Camera;

/// <summary>
/// The framing in use: the chosen shot, eased in from wherever the framing was when it was chosen. A shot asked for
/// only briefly (a manual flickering on over a bump) must hold for <see cref="Settle"/> before it is taken.
/// </summary>
internal sealed class ShotBlend
{
	/// <summary>Seconds a newly asked-for shot must hold before the framing turns to it.</summary>
	internal const float Settle = 0.12f;

	private CameraShot _from;
	private CameraShot _to;
	private CameraShot _asked;
	private float _askedFor;
	private float _elapsed;

	internal CameraShot Value { get; private set; }

	/// <summary>The shot the framing is turning to (or holds).</summary>
	internal CameraShot Target => _to;

	internal void Reset( CameraShot shot )
	{
		Value = _from = _to = _asked = shot;
		_askedFor = 0f;
		_elapsed = float.PositiveInfinity;
	}

	/// <param name="shot">The shot the situation asks for.</param>
	/// <param name="urgent">Taken without waiting to settle (a bail, stepping off).</param>
	internal CameraShot Update( CameraShot shot, bool urgent, float delta )
	{
		_askedFor = shot == _asked ? _askedFor + delta : 0f;
		_asked = shot;
		if ( shot != _to && (urgent || _askedFor >= Settle) )
		{
			_from = Value;
			_to = shot;
			_elapsed = 0f;
		}
		_elapsed += delta;
		Value = _to.TransitionIn > 0f && _elapsed < _to.TransitionIn
			? _from.Lerp( _to, CameraMath.Ease( _elapsed / _to.TransitionIn ) )
			: _to;
		return Value;
	}
}
