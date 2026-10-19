using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Runs retrieval at its supplied fixed rate and observes completion before the next motion update.</summary>
internal sealed class BoardPickupPlayback
{
	private readonly BoardRetrievalInitializer _initializer;
	private readonly BoardRetrievalMotion _motion;
	private readonly float _step;
	private LimbRigidTransform _source;
	private float _duration;
	private float _elapsed;
	private float _pending;

	internal bool Complete { get; private set; }
	internal float Progress { get; private set; }
	internal LimbRigidTransform Frame { get; private set; }

	internal BoardPickupPlayback( BoardRetrievalInitializer initializer, BoardRetrievalMotion motion, float step )
	{
		ArgumentNullException.ThrowIfNull( initializer );
		ArgumentNullException.ThrowIfNull( motion );
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		_initializer = initializer;
		_motion = motion;
		_step = step;
	}

	internal void Begin( LimbRigidTransform board, LimbRigidTransform target, Vector3 fallback )
	{
		var entry = _initializer.Begin( 2, board, board.Translation, target, fallback, false );
		_source = entry.Source;
		_duration = entry.Duration;
		_elapsed = 0f;
		_pending = 0f;
		Progress = 0f;
		Complete = false;
		Frame = _source;
	}

	internal void Advance( float delta, LimbRigidTransform target )
	{
		if ( !float.IsFinite( delta ) || delta < 0f )
			throw new ArgumentOutOfRangeException( nameof( delta ) );
		_pending += delta;
		while ( !Complete && _pending >= _step )
		{
			_pending -= _step;
			// The owner observes progress before dispatching this tick's retrieval motion.
			if ( !(Progress < 1f) )
			{
				Complete = true;
				break;
			}
			var result = _motion.Update( _source, target, _duration, _elapsed );
			_elapsed = result.Elapsed;
			Progress = result.Progress;
			Frame = result.Frame;
		}
	}

	/// <summary>
	/// The board frame for the progress reached so far, blended toward <paramref name="target"/> as it is now; the
	/// target itself once retrieval has reached it. Does not advance retrieval.
	/// </summary>
	internal LimbRigidTransform Sample( LimbRigidTransform target ) =>
		Complete || !(Progress < 1f) ? target : _motion.Sample( _source, target, _duration, _elapsed );
}
