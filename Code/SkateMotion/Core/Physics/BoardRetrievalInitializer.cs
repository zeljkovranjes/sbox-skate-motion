namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;
using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Chooses the retrieval start frame and duration from retained ownership state and distance.</summary>
internal sealed class BoardRetrievalInitializer
{
	private readonly BoardCorrectionCurve _durationCurve;
	private readonly float _minimumDistance;
	private readonly float _distanceLimit;
	private readonly float _mountingDistanceLimit;
	private readonly float _mountingDurationLimit;

	internal BoardRetrievalInitializer( BoardCorrectionCurve durationCurve, float minimumDistance,
		float distanceLimit, float mountingDistanceLimit, float mountingDurationLimit )
	{
		ArgumentNullException.ThrowIfNull( durationCurve );
		if ( !float.IsFinite( minimumDistance ) || minimumDistance <= 0f
			|| !float.IsFinite( distanceLimit ) || distanceLimit < 0f
			|| !float.IsFinite( mountingDistanceLimit ) || mountingDistanceLimit < 0f
			|| !float.IsFinite( mountingDurationLimit ) || mountingDurationLimit < 0f )
			throw new ArgumentException( "Retrieval distances and duration limits must be finite and nonnegative, with a positive minimum distance." );
		_durationCurve = durationCurve;
		_minimumDistance = minimumDistance;
		_distanceLimit = distanceLimit;
		_mountingDistanceLimit = mountingDistanceLimit;
		_mountingDurationLimit = mountingDurationLimit;
	}

	internal BoardRetrievalInitialization Begin( int state, LimbRigidTransform board, Vector3 previousPosition,
		LimbRigidTransform attachment, Vector3 fallbackDirection, bool mounting )
	{
		var source = state == 3 ? board with { Translation = previousPosition } : board;
		var difference = source.Translation - attachment.Translation;
		var distance = SquareRoot( Dot( difference, difference ) );
		if ( distance < _minimumDistance )
		{
			distance = _minimumDistance;
			difference = Scale( fallbackDirection, _minimumDistance );
		}
		var limit = mounting ? _mountingDistanceLimit : _distanceLimit;
		var relocate = limit < distance || state == 3;
		if ( relocate )
		{
			// The retained-position state projects to the limit even when already closer.
			source = source with
			{
				Translation = AddScaled( Scale( difference, limit ), Reciprocal( distance ), attachment.Translation )
			};
			distance = limit;
		}
		var duration = _durationCurve.Evaluate( distance );
		if ( mounting )
			duration = _mountingDurationLimit - duration >= 0f ? duration : _mountingDurationLimit;
		return new( source, duration, relocate );
	}
}
