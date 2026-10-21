namespace SkateMotion.Core.Camera;

/// <summary>
/// Framing for one situation, relative to the designer's follow distance and height. Shots blend into each other
/// with a sine ease over the incoming shot's <see cref="TransitionIn"/>.
/// </summary>
/// <param name="Distance">Follow distance scale.</param>
/// <param name="Elevation">Scale of the resting angle the camera looks down at the focus from.</param>
/// <param name="Focus">Scale of the focus point's height above the board or feet.</param>
/// <param name="Board">1 follows the board, 0 the rider's body (on foot, or a ragdoll).</param>
/// <param name="Follow">Natural frequency (rad/s) of the position follow.</param>
/// <param name="FeedForward">Share of the subject's own velocity fed forward: 0.5 halves the lag behind steady motion
/// and is the most that never runs past a subject that stops dead.</param>
/// <param name="LookAhead">Seconds of travel the view aims ahead of the focus.</param>
/// <param name="TransitionIn">Seconds to blend into this shot.</param>
internal readonly record struct CameraShot( float Distance, float Elevation, float Focus, float Board, float Follow,
	float FeedForward, float LookAhead, float TransitionIn )
{
	/// <summary>Riding: the follow distance and height as set.</summary>
	internal static CameraShot Chase { get; } = new( 1f, 1f, 1f, 1f, 12f, 0.5f, 0.1f, 0.5f );

	/// <summary>Grinding: a little closer and much lower, along the rail.</summary>
	internal static CameraShot Grind { get; } = new( 0.97f, 0.57f, 1f, 1f, 12f, 0.5f, 0.1f, 0.5f );

	/// <summary>Manuals: closer and lower.</summary>
	internal static CameraShot Manual { get; } = new( 0.75f, 0.63f, 1f, 1f, 12f, 0.5f, 0.1f, 0.5f );

	/// <summary>Walking or carrying the board: follows the rider's body, eased into over a second.</summary>
	internal static CameraShot OnFoot { get; } = new( 1f, 1f, 1f, 0f, 12f, 0.5f, 0f, 1f );

	/// <summary>Bailed: pulled back and up, following the tumbling body softly so it never whips.</summary>
	internal static CameraShot Bail { get; } = new( 1.2f, 1.25f, 0.3f, 0f, 3.5f, 0f, 0f, 0.35f );

	/// <summary>Every field moved part of the way toward another shot's (transition time taken from the other).</summary>
	internal CameraShot Lerp( CameraShot to, float amount ) => new(
		Distance + (to.Distance - Distance) * amount,
		Elevation + (to.Elevation - Elevation) * amount,
		Focus + (to.Focus - Focus) * amount,
		Board + (to.Board - Board) * amount,
		Follow + (to.Follow - Follow) * amount,
		FeedForward + (to.FeedForward - FeedForward) * amount,
		LookAhead + (to.LookAhead - LookAhead) * amount,
		to.TransitionIn );
}
