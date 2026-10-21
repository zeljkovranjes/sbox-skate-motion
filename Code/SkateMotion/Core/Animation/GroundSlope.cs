namespace SkateMotion.Core.Animation;

/// <summary>
/// The ground a rider walks on, as the walking clips name it: flat, a ramp or a flight of stairs, up or down, shallow or
/// steep. Classed from how far the ground rises along the way the rider walks, and whether it rises in steps (flat treads
/// between the heights) or as one slope. A class must hold for <see cref="Settle"/> seconds before it is taken, so the
/// gait does not flicker at a kerb or the edge of a ramp.
/// </summary>
internal sealed class GroundSlope
{
	/// <summary>The class for level ground, and the one taken while standing or in the air.</summary>
	internal const string Flat = "flat";

	// Slopes (degrees) below FlatAngle are flat; ramps steeper than SteepRamp, and stairs steeper than SteepStairs, are steep.
	private const float FlatAngle = 6f;
	private const float SteepRamp = 25f;
	private const float SteepStairs = 22f;

	/// <summary>Seconds a new class must hold before it is taken.</summary>
	internal const float Settle = 0.15f;

	private string _pending = Flat;
	private float _held;

	/// <summary>The class taken now.</summary>
	internal string Current { get; private set; } = Flat;

	/// <summary>The class of ground rising <paramref name="rise"/> over <paramref name="run"/> along the way the rider
	/// walks (falling where negative), in steps when <paramref name="stepped"/>.</summary>
	internal static string Classify( float rise, float run, bool stepped )
	{
		if ( !(run > 0f) || !float.IsFinite( rise ) )
			return Flat;
		var angle = MathF.Atan2( MathF.Abs( rise ), run ) * 180f / MathF.PI;
		if ( angle < FlatAngle )
			return Flat;
		var way = rise > 0f ? "up" : "dn";
		return stepped
			? $"stair-{way}-{(angle > SteepStairs ? "steep" : "shallow")}"
			: $"ramp-{way}-{(angle > SteepRamp ? "steep" : "shallow")}";
	}

	// The kind of ground a class names, without its steepness: "stair-up" for "stair-up-steep".
	private static string Kind( string slope )
	{
		var last = slope.LastIndexOf( '-' );
		return last > 0 && (slope.EndsWith( "-steep" ) || slope.EndsWith( "-shallow" )) ? slope[ ..last ] : slope;
	}

	/// <summary>Takes this step's class (<see cref="Flat"/> when the rider is not walking on the ground); returns the class taken.</summary>
	internal string Update( string measured, float delta )
	{
		measured ??= Flat;
		if ( measured == Current )
		{
			_pending = measured;
			_held = 0f;
			return Current;
		}
		// Stairs read from one moment to the next swing between their shallow and steep classes as each tread passes under
		// the samples (the rise they see steps by a riser): the time held counts for the kind of ground (stairs up, a ramp
		// down, ...), and the latest class of it is taken.
		_held = Kind( measured ) == Kind( _pending ) ? _held + delta : 0f;
		_pending = measured;
		if ( _held >= Settle )
		{
			Current = measured;
			_held = 0f;
		}
		return Current;
	}
}
