namespace SkateMotion.Core.Input;

/// <summary>
/// Turns an on/off key pair (-1, 0 or 1) into a stick-like value that moves over time: it rises to full over
/// <see cref="Rise"/> seconds and returns to centre (or crosses it when the other key is pressed) over
/// <see cref="Fall"/> seconds, so a key press steers like a stick pushed over rather than snapping to full lock.
/// </summary>
internal sealed class KeyRamp
{
	internal float Rise { get; }
	internal float Fall { get; }
	internal float Value { get; private set; }

	internal KeyRamp( float rise, float fall )
	{
		Rise = rise;
		Fall = fall;
	}

	internal float Step( float target, float delta )
	{
		var centring = target == 0f || MathF.Sign( target ) != MathF.Sign( Value );
		var step = delta / (centring ? Fall : Rise);
		Value = Value < target ? MathF.Min( Value + step, target ) : MathF.Max( Value - step, target );
		return Value;
	}

	internal void Reset() => Value = 0f;
}
