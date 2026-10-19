namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Combines target limiting, position solving and segment-matrix updates for a two-segment chain.</summary>
internal sealed class LimbChainSolver
{
	private readonly LimbPositionSolver _positions;
	private readonly LimbRotationSolver _rotations;
	private readonly float _minimumNormalLength;

	internal LimbChainSolver( LimbPositionSolver positions, LimbRotationSolver rotations, float minimumNormalLength )
	{
		ArgumentNullException.ThrowIfNull( positions );
		ArgumentNullException.ThrowIfNull( rotations );
		if ( !float.IsFinite( minimumNormalLength ) || minimumNormalLength <= 0f )
			throw new ArgumentException( "The normal threshold must be finite and positive." );
		_positions = positions;
		_rotations = rotations;
		_minimumNormalLength = minimumNormalLength;
	}

	internal LimbChainResult Solve( Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 targetOffset,
		LimbRigidTransform upper, LimbRigidTransform lower, float minimumDegrees, float maximumDegrees,
		float maximumReach )
	{
		var requested = ankle + targetOffset;
		var target = requested;
		var direction = target - hip;
		var distance = SquareRoot( Dot( direction, direction ) );
		if ( !(maximumReach >= distance) )
			target = AddScaled( direction, maximumReach / distance, hip );

		var positions = _positions.Solve( hip, knee, ankle, target, minimumDegrees, maximumDegrees, true, 2 );
		if ( positions.Status == 2 )
			return new LimbChainResult( false, upper, lower, Vector3.Zero );

		// A frame failure still reports target adjustment; only position failure clears the residual.
		var residual = positions.Target - requested;
		var normal = Cross( hip - knee, ankle - knee )
			+ Cross( hip - positions.Knee, positions.Target - positions.Knee );
		var squared = Dot( normal, normal );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		normal = length > _minimumNormalLength ? Scale( normal, inverse ) : Vector3.Zero;

		if ( !_rotations.TrySolve( knee, ankle, normal, positions.Knee, positions.Target, normal, out var lowerDelta )
			|| !_rotations.TrySolve( hip, knee, normal, hip, positions.Knee, normal, out var upperDelta ) )
			return new LimbChainResult( false, upper, lower, residual );

		return new LimbChainResult( true, upperDelta.ApplyToBone( upper ), lowerDelta.ApplyToBone( lower ), residual );
	}
}
