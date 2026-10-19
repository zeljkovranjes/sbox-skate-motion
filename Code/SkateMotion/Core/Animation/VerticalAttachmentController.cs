namespace SkateMotion.Core.Animation;

/// <summary>Produces vertical attachment requests from phase changes and retained height/speed history.</summary>
internal sealed class VerticalAttachmentController
{
	private readonly VerticalAttachmentSettings _settings;
	private readonly VerticalAttachmentCurve _transition;
	private readonly VerticalAttachmentCurve _tracking;
	private readonly VerticalAttachmentCurve _impulse;
	private const float Step = 1f / 60f;
	internal bool Active { get; set; }
	internal int Mode { get; set; }
	internal float Elapsed { get; set; }
	internal float Height { get; set; }
	internal float Velocity { get; set; }
	internal float PreviousSpeed { get; set; }
	internal float PreviousHeight { get; set; } = float.MaxValue;
	internal int PreviousCategory { get; set; }
	internal float Baseline { get; set; }

	internal VerticalAttachmentController( VerticalAttachmentSettings settings,
		VerticalAttachmentCurve transition, VerticalAttachmentCurve tracking, VerticalAttachmentCurve impulse )
	{
		ArgumentNullException.ThrowIfNull( transition );
		ArgumentNullException.ThrowIfNull( tracking );
		ArgumentNullException.ThrowIfNull( impulse );
		_settings = settings;
		_transition = transition;
		_tracking = tracking;
		_impulse = impulse;
	}

	internal void Update( VerticalAttachmentInput input, BoardAttachmentController attachment )
	{
		ArgumentNullException.ThrowIfNull( attachment );
		var primary = (PreviousCategory == 2 || PreviousCategory == 6)
			&& input.Category == 1 && input.PrimaryActive;
		var transition = PreviousCategory == 2 && input.Category == 1 && input.Trigger != 0f;
		var tracking = PreviousCategory == 2 && input.Category == 3 && !input.StopTracking;
		var impulse = input.SecondaryActive && (tracking || (PreviousCategory == 2 && input.Category == 1));
		var initialPrimary = false;
		if ( input.Suppress && !input.SecondaryActive )
		{
			Active = false;
		}
		else if ( !Active && (primary || transition || tracking || impulse) )
		{
			Active = true;
			Elapsed = 0f;
			Mode = impulse ? 3 : transition ? 2 : primary ? 0 : 1;
			initialPrimary = Mode == 0;
			Velocity = Bound( tracking || impulse ? PreviousSpeed : input.ProjectedSpeed, -_settings.MaximumSpeed, 0f );
			PreviousHeight = float.MaxValue;
			Height = input.CachedHeight;
		}

		if ( Active )
			Advance( input, attachment, initialPrimary );
		PreviousSpeed = input.ProjectedSpeed;
		PreviousCategory = input.Category;
	}

	private void Advance( VerticalAttachmentInput input, BoardAttachmentController attachment, bool initialPrimary )
	{
		var baseline = _settings.RestHeight;
		var damping = 0f;
		var spring = 0f;
		var weight = 0f;
		var stop = false;
		var upperSpeed = _settings.MaximumSpeed;
		switch ( Mode )
		{
			case 0:
				damping = _settings.PrimaryDamping;
				spring = _settings.PrimarySpring;
				weight = 1f;
				upperSpeed = 0f;
				stop = input.Category != 1 || !input.PrimaryActive;
				break;
			case 1:
				damping = _settings.TrackingDamping;
				spring = _settings.TrackingSpring;
				weight = _tracking.Evaluate( Elapsed );
				stop = input.Category != 3;
				if ( Elapsed > _settings.TrackingFollowDelay )
					baseline = Bound( input.CurrentHeight, Baseline - _settings.TrackingStep, Baseline + _settings.TrackingStep );
				Baseline = baseline;
				break;
			case 2:
				damping = _settings.TransitionDamping;
				spring = _settings.TransitionSpring;
				weight = _transition.Evaluate( Elapsed );
				stop = input.Category != 1;
				break;
			case 3:
				weight = 1f;
				stop = input.Category != 1 || Elapsed > _settings.ImpulseDuration || !input.SecondaryActive;
				break;
		}

		if ( Mode == 3 )
		{
			var gain = Bound( MathF.Abs( Velocity ) / _settings.ImpulseSpeedScale, 0f, 1f );
			Height = MathF.FusedMultiplyAdd( _impulse.Evaluate( Elapsed / _settings.ImpulseDuration ), gain, _settings.ImpulseHeight );
		}
		else
		{
			var difference = Height - baseline;
			var acceleration = -MathF.FusedMultiplyAdd( spring, difference, damping * Velocity );
			var velocity = Bound( MathF.FusedMultiplyAdd( acceleration, Step, Velocity ), -_settings.MaximumSpeed, upperSpeed );
			var integrated = difference + MathF.FusedMultiplyAdd( Velocity + velocity, 1f / 120f, baseline );
			Height = integrated - _settings.MinimumHeight >= 0f ? integrated : _settings.MinimumHeight;
			Velocity = velocity;
		}

		var correction = input.CurrentHeight - Height;
		if ( initialPrimary )
		{
			correction += Bound( Velocity * .015f, -.1f, 0f );
			correction = correction >= 0f ? correction : 0f;
		}
		if ( Mode == 0 && Elapsed > _settings.PrimaryStopDelay && PreviousHeight < input.CurrentHeight && correction > 0f )
			stop = true;
		if ( Mode == 1 && input.StopTracking )
			stop = true;
		if ( input.ForceRequest || (Elapsed <= 1.5f && !stop && weight >= .05f) )
		{
			var updates = input.ForceRequest ? 6f : Mode == 3 ? _settings.ImpulseReleaseUpdates : 15f;
			attachment.RequestHeight( weight * correction, updates );
			if ( input.ForceRequest )
				Active = false;
		}
		else
		{
			Active = false;
		}
		Elapsed += Step;
		PreviousHeight = input.CurrentHeight;
	}

	private static float Bound( float value, float minimum, float maximum )
	{
		var lower = minimum - value >= 0f ? minimum : value;
		return maximum - lower >= 0f ? lower : maximum;
	}
}
