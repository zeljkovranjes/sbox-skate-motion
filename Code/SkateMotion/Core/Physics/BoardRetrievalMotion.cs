namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;

/// <summary>Advances retrieval progress and blends the initial board frame toward the current attachment.</summary>
internal sealed class BoardRetrievalMotion
{
	private readonly BoardCorrectionCurve _curve;
	private readonly LimbPoseBlender _blender;
	private readonly float _step;

	internal BoardRetrievalMotion( BoardCorrectionCurve curve, LimbPoseBlender blender, float step )
	{
		ArgumentNullException.ThrowIfNull( curve );
		ArgumentNullException.ThrowIfNull( blender );
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentException( "Retrieval step must be finite and positive.", nameof( step ) );
		_curve = curve;
		_blender = blender;
		_step = step;
	}

	internal BoardRetrievalResult Update( LimbRigidTransform source, LimbRigidTransform attachment,
		float duration, float elapsed )
	{
		elapsed += _step;
		var progress = elapsed / duration;
		// Preserve the upper-only selection, including unordered division results.
		progress = 1f - progress >= 0f ? progress : 1f;
		var weight = _curve.Evaluate( progress );
		return new( elapsed, progress, weight, _blender.BlendTransform( source, attachment, weight ) );
	}

	/// <summary>
	/// The frame at an already reached elapsed time toward a supplied attachment, without advancing the clock. A
	/// presenter calls it between fixed updates with the attachment as it is now, so a frame shown at another rate
	/// than the clock's uses the same weight and blend instead of an attachment one update old.
	/// </summary>
	internal LimbRigidTransform Sample( LimbRigidTransform source, LimbRigidTransform attachment,
		float duration, float elapsed )
	{
		var progress = elapsed / duration;
		progress = 1f - progress >= 0f ? progress : 1f;
		return _blender.BlendTransform( source, attachment, _curve.Evaluate( progress ) );
	}
}
