namespace SkateMotion.Core.Input;

using Vector2 = System.Numerics.Vector2;

/// <summary>An ordered stick path supplied by a controller profile.</summary>
internal sealed class GesturePattern
{
	private readonly Vector2[] _points;

	internal float SquaredTolerance { get; }
	internal int Count => _points.Length;
	internal Vector2 this[ int index ] => _points[ index ];

	internal GesturePattern( Vector2[] points, float squaredTolerance )
	{
		ArgumentNullException.ThrowIfNull( points );
		if ( points.Length < 2 || points.Length > 15 )
			throw new ArgumentOutOfRangeException( nameof( points ) );
		if ( !float.IsFinite( squaredTolerance ) || squaredTolerance < 0f )
			throw new ArgumentOutOfRangeException( nameof( squaredTolerance ) );
		for ( var index = 0; index < points.Length; index++ )
		{
			if ( !float.IsFinite( points[ index ].X ) || !float.IsFinite( points[ index ].Y ) )
				throw new ArgumentException( "Path coordinates must be finite.", nameof( points ) );
		}
		_points = points.AsSpan().ToArray();
		SquaredTolerance = squaredTolerance;
	}
}
