namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Prepares a chosen trajectory with anchored position and curve-scaled, speed-limited velocity correction.</summary>
internal sealed class GrindTrajectoryPreparation
{
	private readonly GrindEdgeSelectionCurve _curve;
	private readonly GrindContactCorrection _correction;

	internal GrindTrajectoryPreparation( GrindEdgeSelectionCurve curve, GrindContactCorrection correction )
	{
		ArgumentNullException.ThrowIfNull( curve );
		ArgumentNullException.ThrowIfNull( correction );
		_curve = curve;
		_correction = correction;
	}

	internal GrindTrajectoryState Prepare( GrindTrajectoryState selected, Vector4 direction, Vector4 anchor,
		Vector4 contact, Vector4 projection, int sampleIndex, float maximumSpeed )
	{
		var gain = _curve.EvaluateOrdered( direction.Y );
		var offset = anchor - selected.Position;
		var relative = contact - anchor;
		var distance = MathF.Abs( Dot( new Vector3( projection.X, projection.Y, projection.Z ),
			new Vector3( relative.X, relative.Y, relative.Z ) ) );
		var amount = MultiplyAdd( gain, distance, 0f );
		var desired = new Vector4( MultiplyAdd( direction.X, amount, 0f ), MultiplyAdd( direction.Y, amount, 0f ),
			MultiplyAdd( direction.Z, amount, 0f ), MultiplyAdd( direction.W, amount, 0f ) );
		if ( sampleIndex <= 0 )
			return selected;
		return selected with
		{
			Position = selected.Position + offset,
			Velocity = _correction.Apply( selected.Velocity, desired - offset, sampleIndex, maximumSpeed )
		};
	}
}
