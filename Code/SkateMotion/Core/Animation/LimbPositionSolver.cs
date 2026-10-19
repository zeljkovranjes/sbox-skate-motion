namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Reconstructs a limb's bend plane and retries targets outside caller-supplied angular limits.</summary>
internal sealed class LimbPositionSolver
{
	private readonly LimbSolverMath _math;
	private readonly float _epsilon;
	private readonly float _degreesToRadians;
	private readonly float _radiansToDegrees;

	internal LimbPositionSolver( LimbSolverMath math, float epsilon, float degreesToRadians, float radiansToDegrees )
	{
		ArgumentNullException.ThrowIfNull( math );
		if ( !float.IsFinite( epsilon ) || epsilon <= 0f
			|| !float.IsFinite( degreesToRadians ) || degreesToRadians <= 0f
			|| !float.IsFinite( radiansToDegrees ) || radiansToDegrees <= 0f )
			throw new ArgumentException( "Limb distance and angular scales must be finite and positive." );
		_math = math;
		_epsilon = epsilon;
		_degreesToRadians = degreesToRadians;
		_radiansToDegrees = radiansToDegrees;
	}

	/// <summary>Solves positions only; singular input can produce nonfinite output and needs a caller-side validity policy.</summary>
	internal LimbPositionResult Solve( Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 target,
		float minimumDegrees, float maximumDegrees, bool relaxMaximum, int attempts )
	{
		while ( attempts > 0 )
		{
			var upper = knee - hip;
			var lower = ankle - knee;
			var upperLength = SquareRoot( Dot( upper, upper ) );
			var lowerLength = SquareRoot( Dot( lower, lower ) );
			if ( upperLength < _epsilon || lowerLength < _epsilon )
				return new LimbPositionResult( 2, knee, target );

			var axis = Normalize( ankle - hip );
			var direction = Normalize( target - hip );
			var normal = Normalize( Cross( upper, lower ) );
			var oldFrame = new LimbFrame( axis, Cross( axis, normal ), normal );
			var side = Cross( direction, normal );
			var newFrame = new LimbFrame( direction, side, Cross( side, direction ) );
			var oldInverse = oldFrame.Inverse();
			var oldTranslation = oldInverse.Transform( -hip, Vector3.Zero );
			var localHip = oldInverse.Transform( hip, oldTranslation );
			var localKnee = oldInverse.Transform( knee, oldTranslation );
			var localAnkle = oldInverse.Transform( ankle, oldTranslation );
			var newInverse = newFrame.Inverse();
			var newTranslation = newInverse.Transform( -hip, Vector3.Zero );
			var localTarget = newInverse.Transform( target, newTranslation );
			var reach = localTarget - localHip;
			var distanceSquared = MultiplyAdd( reach.X, reach.X, MultiplyAdd( reach.Y, reach.Y, 0f ) );
			var distance = SquareRoot( distanceSquared );

			var animatedCosine = MultiplyAdd( Reciprocal( MultiplyAdd( upperLength, -lowerLength, 0f ) ),
				Dot( upper, -lower ), 0f );
			var animatedDegrees = MultiplyAdd( _math.ArcCosine( animatedCosine ), _radiansToDegrees, 0f );
			var maximum = relaxMaximum && animatedDegrees > maximumDegrees ? animatedDegrees : maximumDegrees;
			var upperSquared = MultiplyAdd( upperLength, upperLength, 0f );
			var lowerSquared = MultiplyAdd( lowerLength, lowerLength, 0f );
			var squaredSum = upperSquared + lowerSquared;
			var twiceUpper = MultiplyAdd( 2f, upperLength, 0f );
			var twiceProduct = MultiplyAdd( twiceUpper, lowerLength, 0f );
			var minimumReach = AngularReach( minimumDegrees, squaredSum, twiceProduct );
			var maximumReach = AngularReach( maximum, squaredSum, twiceProduct );

			if ( distance < minimumReach || distance > maximumReach )
			{
				if ( distance > upperLength + lowerLength && maximum == 180f )
				{
					var straightDirection = Normalize( reach );
					var straightKnee = AddScaled( straightDirection, upperLength, localHip );
					var straightAnkle = AddScaled( straightDirection, lowerLength, straightKnee );
					return new LimbPositionResult( 1, newFrame.Transform( straightKnee, hip ),
						newFrame.Transform( straightAnkle, hip ) );
				}
				var adjustedReach = distance > maximumReach ? maximumReach - _epsilon : minimumReach + _epsilon;
				target = AddScaled( Normalize( target - hip ), adjustedReach, hip );
				attempts--;
				continue;
			}

			var jointCosine = MultiplyAdd( Reciprocal( twiceProduct ), squaredSum - distanceSquared, 0f );
			var jointDegrees = MultiplyAdd( _math.ArcCosine( ClampCosine( jointCosine ) ), _radiansToDegrees, 0f );
			if ( minimumDegrees >= jointDegrees )
			{
				target = AddScaled( Normalize( target - hip ), minimumReach + _epsilon, hip );
				attempts--;
				continue;
			}

			var orientation = _math.ArcCosine( ClampCosine( MultiplyAdd( Reciprocal( distance ), reach.X, 0f ) ) );
			if ( (localKnee.Y < 0f && localTarget.Y > 0f) || (localKnee.Y > 0f && localTarget.Y < 0f) )
				orientation = MultiplyAdd( -1f, orientation, 0f );
			var numerator = (upperSquared + distanceSquared) - lowerSquared;
			var cosine = MultiplyAdd( Reciprocal( MultiplyAdd( twiceUpper, distance, 0f ) ), numerator, orientation );
			var angle = _math.ArcCosine( ClampCosine( cosine ) );
			if ( localAnkle.Y > localKnee.Y )
				angle = 2f * _math.Pi - angle;
			var rotated = RotateInPlane( localAnkle - localHip, angle );
			var solvedKnee = AddScaled( Normalize( rotated ), upperLength, localHip );
			return new LimbPositionResult( 0, newFrame.Transform( solvedKnee, hip ), target );
		}
		return new LimbPositionResult( 2, knee, ankle );
	}

	private float AngularReach( float degrees, float squaredSum, float twiceProduct )
	{
		var (_, cosine) = _math.SinCos( MultiplyAdd( degrees, _degreesToRadians, 0f ) );
		return SquareRoot( squaredSum - MultiplyAdd( twiceProduct, cosine, 0f ) );
	}

	private Vector3 RotateInPlane( Vector3 vector, float angle )
	{
		var (sine, cosine) = _math.SinCos( angle );
		return new Vector3(
			MultiplyAdd( -sine, vector.Y, MultiplyAdd( cosine, vector.X, 0f ) ),
			MultiplyAdd( cosine, vector.Y, MultiplyAdd( sine, vector.X, 0f ) ), vector.Z );
	}
}
