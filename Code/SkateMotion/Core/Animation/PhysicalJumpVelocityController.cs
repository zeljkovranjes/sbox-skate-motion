namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Computes a surface-dependent launch and publishes its velocity to every supplied body.</summary>
internal static class PhysicalJumpVelocityController
{
	internal static Vector3 Launch( PhysicalAnimationAttributeState attributes,
		PhysicalJumpVelocityState state, PhysicalJumpVelocityInput input,
		PhysicalJumpVelocityTuning tuning, ReadOnlySpan<PhysicalBodyState> bodies )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( state );
		var direction = LaunchDirection( input );
		var speed = InterpolateSpeed( attributes.JumpStrength, tuning.MinimumSpeed, tuning.MaximumSpeed );
		var velocity = input.Velocity - Scale( direction, Dot( input.Velocity, direction ) );
		velocity = AddScaled( direction, speed * 1.03f, velocity );

		var side = Cross( input.SecondaryDirection, direction );
		var lateral = LateralSpeed( attributes, state, input, side );
		var influence = input.CorrectionEnabled ? tuning.CorrectionInfluence : 0f;
		var correction = Scale( side, lateral ) - Scale( input.Correction, influence );
		var length = SquareRoot( Dot( correction, correction ) );
		if ( length > 1.8f )
			correction = Scale( correction, 1.8f / length );
		velocity += correction;
		foreach ( var body in bodies )
			body.Velocity = velocity;

		state.Triggered = true;
		var remaining = state.Cooldown - .6f;
		state.Cooldown = -remaining >= 0f ? 0f : remaining;
		return velocity;
	}

	internal static float InterpolateSpeed( float strength, float minimum, float maximum ) =>
		MultiplyAdd( strength, maximum, (1f - strength) * minimum );

	private static Vector3 LaunchDirection( PhysicalJumpVelocityInput input )
	{
		Vector3 candidate;
		if ( input.Mode == 2 || input.Mode == 4 )
		{
			var cross = Cross( input.PrimaryDirection, input.SecondaryDirection );
			if ( !(Dot( cross, input.Position - input.Anchor ) > 0f) )
				cross = -cross;
			var bias = input.Mode == 4 ? -.1093f : .291f;
			candidate = input.PrimaryDirection + Scale( cross, bias );
		}
		else
		{
			candidate = Scale( input.FallbackDirection, .5f ) + Scale( input.PrimaryDirection, .5f );
		}
		var length = SquareRoot( Dot( candidate, candidate ) );
		return length > .1f ? Scale( candidate, Reciprocal( length ) ) : input.PrimaryDirection;
	}

	private static float LateralSpeed( PhysicalAnimationAttributeState attributes,
		PhysicalJumpVelocityState state, PhysicalJumpVelocityInput input, Vector3 side )
	{
		var control = attributes.GrindStabilityNudge;
		if ( !(state.Cooldown < .31f) )
			return control * 1.8f;

		float lateral;
		if ( control < 0f )
			lateral = (control + .42f >= 0f ? -.42f : control) * 1.8f;
		else if ( control > 0f )
			lateral = (control - .42f >= 0f ? control : .42f) * 1.8f;
		else
			lateral = Dot( input.Position - input.ReferencePosition, side ) > 0f ? .756f : -.756f;
		state.HoldFrames = 10;
		attributes.ActionFlags |= 1UL << 25;
		return lateral;
	}
}
