namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Adjusts the first two prepared effector poses using cached contact heights.</summary>
internal sealed class FootReachController
{
	private readonly FootReachSettings _settings;

	internal FootReachController( FootReachSettings settings )
	{
		if ( !float.IsFinite( settings.MinimumRequest ) || !float.IsFinite( settings.MaximumRequest )
			|| !float.IsFinite( settings.MinimumStep ) || !float.IsFinite( settings.MaximumStep )
			|| !float.IsFinite( settings.MinimumCorrection ) || !float.IsFinite( settings.ContactBias )
			|| !float.IsFinite( settings.MaximumContactHeight ) || !float.IsFinite( settings.Clearance )
			|| !float.IsFinite( settings.ReferenceHeightThreshold ) || !float.IsFinite( settings.FlagDistanceThreshold )
			|| !float.IsFinite( settings.FlagOffsetThreshold ) || settings.MinimumRequest > settings.MaximumRequest
			|| settings.MinimumStep > settings.MaximumStep || settings.MinimumCorrection > 0f )
			throw new ArgumentException( "Reach settings must be finite with ordered bounds and a nonpositive correction floor." );
		_settings = settings;
	}

	internal FootReachFlags Update( FootReachInput[] inputs, FootReachState[] states, EffectorPoseState[] poses,
		int selectedId, LimbRigidTransform contactFrame, LimbFrame offsetOrientation, float referenceHeight,
		bool reactionEnabled, bool requestSuppressed, FootReachFlags flags )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( poses );
		if ( inputs.Length != 2 || states.Length != 2 || poses.Length != 4 )
			throw new ArgumentException( "Reach adjustment needs two foot inputs/states and four pose slots." );
		flags = flags with { Frame = false };
		for ( var i = 0; i < states.Length; i++ )
		{
			var state = states[ i ];
			var pose = poses[ i ];
			var input = inputs[ i ];
			var trigger = false;
			if ( pose.TargetState.Mode != 1 || input.TargetId != selectedId )
			{
				state = Smooth( state with { ContactState = 0 }, 0f );
				pose = ApplyOffset( pose, state.Smoothed, offsetOrientation );
			}
			else
			{
				if ( state.ContactState == 1 )
				{
					AdjustTracked( ref state, ref pose, contactFrame, offsetOrientation, referenceHeight,
						reactionEnabled, requestSuppressed, out trigger );
				}
				else if ( state.ContactState is 0 or 2 )
				{
					state = Smooth( state, state.Requested );
					pose = ApplyOffset( pose, state.Smoothed, offsetOrientation );
					trigger = state.ContactState == 2 && reactionEnabled;
				}
				// The old cached contact drives this frame; fresh contact becomes next frame's input.
				state = input.Contact.HasValue
					? state with { ContactState = 1, CachedContact = input.Contact.Value }
					: state with { ContactState = 2 };
			}
			if ( trigger )
				flags = new FootReachFlags( true, true );
			states[ i ] = state;
			poses[ i ] = pose;
		}
		return flags;
	}

	private void AdjustTracked( ref FootReachState state, ref EffectorPoseState pose,
		LimbRigidTransform contactFrame, LimbFrame offsetOrientation, float referenceHeight,
		bool reactionEnabled, bool requestSuppressed, out bool trigger )
	{
		var height = contactFrame.Transform( state.CachedContact ).Y + _settings.ContactBias;
		if ( height > _settings.MaximumContactHeight )
		{
			state = Smooth( state, state.Requested );
			pose = ApplyOffset( pose, state.Smoothed, offsetOrientation );
			trigger = reactionEnabled || referenceHeight > _settings.ReferenceHeightThreshold;
			return;
		}
		state = Smooth( state, requestSuppressed ? 0f : height );
		var offset = Offset( state.Smoothed, offsetOrientation );
		var targetHeight = contactFrame.Transform( pose.AnimatedTarget.Translation + offset ).Y;
		var contactHeight = contactFrame.Transform( state.CachedContact ).Y;
		var distance = (targetHeight - _settings.Clearance) - contactHeight;
		if ( distance < 0f && distance > _settings.MinimumCorrection )
			state = state with { Smoothed = state.Smoothed - distance };
		pose = ApplyOffset( pose, state.Smoothed, offsetOrientation );
		trigger = reactionEnabled && distance > _settings.FlagDistanceThreshold && state.Smoothed < _settings.FlagOffsetThreshold;
	}

	private FootReachState Smooth( FootReachState state, float requested )
	{
		requested = OrderedClamp( requested, _settings.MinimumRequest, _settings.MaximumRequest );
		var step = OrderedClamp( requested - state.Smoothed, _settings.MinimumStep, _settings.MaximumStep );
		return state with { Requested = requested, Smoothed = state.Smoothed + step };
	}

	private static float OrderedClamp( float value, float lower, float upper )
	{
		value = lower - value >= 0f ? lower : value;
		return upper - value >= 0f ? value : upper;
	}

	private static Vector3 Offset( float height, LimbFrame orientation ) =>
		orientation.Transform( new Vector3( 0f, height, 0f ), Vector3.Zero );

	private static EffectorPoseState ApplyOffset( EffectorPoseState pose, float height, LimbFrame orientation )
	{
		var offset = Offset( height, orientation );
		return pose with
		{
			AnimatedTarget = pose.AnimatedTarget with { Translation = pose.AnimatedTarget.Translation + offset },
			AnimatedReference = pose.AnimatedReference with { Translation = pose.AnimatedReference.Translation + offset }
		};
	}
}
