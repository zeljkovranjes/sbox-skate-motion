namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Updates selected-contact capture status and decides whether another selection pass is eligible.</summary>
internal sealed class GrindCaptureEligibility
{
	private readonly LimbSignedAngle _angle;
	private readonly float _minimumAngle;

	internal GrindCaptureEligibility( LimbSignedAngle angle, float minimumAngle )
	{
		ArgumentNullException.ThrowIfNull( angle );
		_angle = angle;
		_minimumAngle = minimumAngle;
	}

	internal bool Evaluate( GrindCaptureInput input, ref bool capture )
	{
		if ( input.Suppressed )
			return false;
		var angle = _angle.Evaluate( input.SelectedDirection, input.ReferenceDirection, Vector3.Zero );
		capture = input.ProducerAccepted && input.SelectedIndex == 0;
		return input.UpdateCount == 1 && !(_minimumAngle > angle) && input.CandidateCount > 1 && !capture;
	}
}
