namespace SkateMotion.Core.Vehicle;

/// <summary>
/// A turn toward an angle still to go that takes its speed up and lets it go at a set rate (degrees per second squared),
/// up to a top rate, so a body starts and stops turning smoothly rather than at full speed in one frame. It never turns
/// past the angle left, nor the other way.
/// </summary>
internal sealed class EasedTurn
{
	private readonly float _top;
	private readonly float _speedUp;

	/// <param name="top">Fastest turn, degrees per second.</param>
	/// <param name="speedUp">How quickly the turn gathers and loses speed, degrees per second squared.</param>
	internal EasedTurn( float top, float speedUp )
	{
		_top = top > 0f ? top : throw new ArgumentOutOfRangeException( nameof( top ) );
		_speedUp = speedUp > 0f ? speedUp : throw new ArgumentOutOfRangeException( nameof( speedUp ) );
	}

	/// <summary>Degrees per second the turn runs at now (left positive).</summary>
	internal float Rate { get; private set; }

	/// <summary>Degrees to turn over the next <paramref name="delta"/> seconds toward <paramref name="remaining"/> degrees still to go.</summary>
	internal float Step( float remaining, float delta )
	{
		if ( !(delta > 0f) || !float.IsFinite( remaining ) )
			return 0f;
		// The fastest the turn may run and still come to rest within the angle left.
		var settle = MathF.Sqrt( 2f * _speedUp * MathF.Abs( remaining ) );
		var wanted = MathF.Sign( remaining ) * MathF.Min( _top, settle );
		var change = _speedUp * delta;
		Rate = Math.Clamp( wanted, Rate - change, Rate + change );
		var step = Rate * delta;
		return remaining >= 0f ? Math.Clamp( step, 0f, remaining ) : Math.Clamp( step, remaining, 0f );
	}

	/// <summary>Stops the turn.</summary>
	internal void Reset() => Rate = 0f;
}
