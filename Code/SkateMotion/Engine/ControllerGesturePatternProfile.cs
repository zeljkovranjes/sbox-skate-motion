using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>One locally supplied gesture path and its command name.</summary>
internal sealed class ControllerGesturePatternProfile
{
	public string Name { get; set; }
	public float SquaredTolerance { get; set; }
	public float[][] Points { get; set; }

	internal GesturePattern CreatePattern()
	{
		if ( Points is null )
			throw new ArgumentException( "Gesture path coordinates are missing." );
		var points = new System.Numerics.Vector2[ Points.Length ];
		for ( var index = 0; index < points.Length; index++ )
		{
			if ( Points[ index ]?.Length != 2 )
				throw new ArgumentException( "Gesture coordinates require two components." );
			points[ index ] = new( Points[ index ][ 0 ], Points[ index ][ 1 ] );
		}
		return new( points, SquaredTolerance );
	}
}
