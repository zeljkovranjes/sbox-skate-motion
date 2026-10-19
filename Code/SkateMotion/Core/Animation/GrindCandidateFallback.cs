namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Scores distance to retained edge lines and endpoints when direct acceptance is unavailable.</summary>
internal sealed class GrindCandidateFallback
{
	private readonly float _minimumLength;
	private readonly float _maximumSquaredDistance;
	private readonly float _missingDistance;
	private readonly GrindEdgeSelectionCurve _curve;

	internal GrindCandidateFallback( float minimumLength, float maximumSquaredDistance,
		float missingDistance, GrindEdgeSelectionCurve curve )
	{
		ArgumentNullException.ThrowIfNull( curve );
		_minimumLength = minimumLength;
		_maximumSquaredDistance = maximumSquaredDistance;
		_missingDistance = missingDistance;
		_curve = curve;
	}

	internal float Evaluate( bool foundEdges, Vector4 point, Vector4 time, GrindEdgeRecord[] edges, int count )
	{
		if ( !foundEdges )
			return _curve.EvaluateOrdered( _missingDistance );
		ArgumentNullException.ThrowIfNull( edges );
		if ( count < 0 || count > edges.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		var position = time.X >= 0f && time.Y >= 0f && time.Z >= 0f && time.W >= 0f
			? new Vector3( point.X, point.Y, point.Z ) : Vector3.Zero;
		var minimum = _maximumSquaredDistance;
		for ( var i = 0; i < count; i++ )
		{
			var edge = edges[ i ].Segment;
			var difference = edge.End - edge.Start;
			var squaredLength = Dot( difference, difference );
			var inverseLength = InverseSquareRoot( squaredLength );
			var length = squaredLength == 0f ? 0f : MultiplyAdd( squaredLength, inverseLength, 0f );
			var direction = length > _minimumLength ? Scale( difference, inverseLength ) : Vector3.Zero;
			var fromStart = position - edge.Start;
			var fromEnd = position - edge.End;
			// Projection remains on the infinite line; clamping to the endpoints changes the score.
			var residual = fromStart - Scale( direction, Dot( direction, fromStart ) );
			var squaredDistance = MathF.Min( Dot( residual, residual ), Dot( fromStart, fromStart ) );
			squaredDistance = MathF.Min( squaredDistance, Dot( fromEnd, fromEnd ) );
			minimum = MathF.Min( squaredDistance, minimum );
		}
		return _curve.EvaluateOrdered( SquareRoot( minimum ) );
	}
}
