namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Produces board response, angular correction, contact force and queued speed correction in order.</summary>
internal sealed class PhysicalBoardForceProducer
{
	private readonly LimbSolverMath _math;
	private readonly LimbSignedAngle _angles;
	private readonly BoardCorrectionCurve _firstTilt;
	private readonly BoardCorrectionCurve _secondTilt;
	private readonly BoardCorrectionCurve _heading;

	internal PhysicalBoardForceProducer( LimbSolverMath math, LimbSignedAngle angles,
		BoardCorrectionCurve firstTilt, BoardCorrectionCurve secondTilt, BoardCorrectionCurve heading )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( angles );
		ArgumentNullException.ThrowIfNull( firstTilt );
		ArgumentNullException.ThrowIfNull( secondTilt );
		ArgumentNullException.ThrowIfNull( heading );
		_math = math;
		_angles = angles;
		_firstTilt = firstTilt;
		_secondTilt = secondTilt;
		_heading = heading;
	}

	internal void Update( PhysicalBoardForceState state, PhysicalBoardForceInput input,
		PhysicalBoardForceTuning tuning, PhysicalBodyBinding boardBinding,
		PhysicalBodyState primaryBody, PhysicalBoardForceQueue queue )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( boardBinding );
		ArgumentNullException.ThrowIfNull( boardBinding.Body );
		ArgumentNullException.ThrowIfNull( primaryBody );
		ArgumentNullException.ThrowIfNull( queue );
		UpdateResponse( state, input, tuning.ResponseGain );
		var relative = input.ReferenceFrame.RigidInverse().Compose( boardBinding.Read() );
		state.RelativeRotation = PhysicalPoseOutput.Rotation( relative.Rotation.Orthonormalized() );
		state.HasRelativeRotation = true;
		ApplyAngularCorrection( boardBinding.Body, AngularCorrection( input ) );
		PhysicalBoardForceConsumer.ApplyAtLever( primaryBody, Scale( input.SurfaceNormal, -40f ),
			input.PhysicalFrame.Translation - boardBinding.Read().Translation );
		if ( TrySpeedCorrection( input, tuning, out var force, out _, out _ ) )
			queue.TryAppend( force );
	}

	internal bool TrySpeedCorrection( PhysicalBoardForceInput input, PhysicalBoardForceTuning tuning,
		out PhysicalBoardForce force, out Vector3 headingCorrection, out Vector3 candidateVelocity )
	{
		force = default;
		headingCorrection = candidateVelocity = Vector3.Zero;
		if ( (input.ActionFlags & (1UL << 49)) == 0 )
			return false;
		var projected = input.TargetDirection - Scale( input.FilteredDirection,
			Dot( input.TargetDirection, input.FilteredDirection ) );
		var length = SquareRoot( Dot( projected, projected ) );
		if ( length < 1f / 65536f )
			return false;
		var direction = Scale( projected, 1f / length );
		var current = Dot( input.Velocity, direction );
		candidateVelocity = AddScaled( direction, tuning.TargetSpeed,
			input.Velocity - Scale( direction, current ) );
		var delta = Scale( candidateVelocity - input.Velocity, tuning.VelocityGain );
		if ( tuning.TargetSpeed < current )
			delta = Vector3.Zero;
		var acceleration = Scale( Scale( delta, input.Mass ), Reciprocal( input.DeltaTime ) );
		var magnitude = SquareRoot( Dot( delta, delta ) );
		var active = magnitude >= .01f;
		var result = Vector3.Zero;
		if ( active )
			result = Scale( acceleration, magnitude > tuning.MaximumVelocityChange
				? tuning.MaximumVelocityChange / magnitude : 1f );
		force = new( 15, result, new Vector3( 0f, tuning.ApplicationHeight, 0f ) );
		var angle = _angles.Wrapped( input.TravelDirection, direction, input.SurfaceNormal );
		var response = _heading.Evaluate( MathF.Abs( angle ) * .31830987334251404f );
		headingCorrection = Scale( input.SurfaceNormal, angle >= 0f ? response : -response );
		return active;
	}

	internal Vector3 AngularCorrection( PhysicalBoardForceInput input )
	{
		if ( (input.MotionFlags & (1UL << 29)) != 0 )
			return Vector3.Zero;
		var x = input.PhysicalFrame.Rotation.X;
		var z = input.PhysicalFrame.Rotation.Z;
		var projectedX = NormalizeProjected( x, input.SurfaceNormal );
		var projectedZ = NormalizeProjected( z, input.SurfaceNormal );
		var firstAngle = ScaleAngle( Dot( z, projectedZ ) );
		var secondAngle = ScaleAngle( Dot( x, projectedX ) );
		var firstSign = Dot( x, Cross( z, projectedZ ) ) >= 0f ? 1f : -1f;
		var secondSign = Dot( z, Cross( x, projectedX ) ) >= 0f ? 1f : -1f;
		var first = _firstTilt.Evaluate( firstAngle ) * firstSign;
		first = (input.LateralSuppression == 0f ? 1f : 0f) * first;
		var second = _secondTilt.Evaluate( secondAngle ) * secondSign;
		return AddScaled( x, first, Scale( z, second ) );
	}

	internal static void ApplyAngularCorrection( PhysicalBodyState body, Vector3 correction )
	{
		var inverseInertia = PhysicalBoardForceConsumer.InverseInertia( body );
		var impulse = inverseInertia.Inverse().Transform( Scale( correction, Reciprocal( 1f / 60f ) ), Vector3.Zero );
		impulse = Scale( impulse, Reciprocal( 1f / 60f ) );
		body.AngularAccumulator += inverseInertia.Transform( impulse, Vector3.Zero );
		body.AccumulatorState = 0;
	}

	private float ScaleAngle( float cosine ) => MultiplyAdd( .6366197466850281f,
		_math.ArcCosine( Math.Clamp( cosine, 0f, 1f ) ), 0f );

	private static Vector3 NormalizeProjected( Vector3 axis, Vector3 normal )
	{
		var projected = axis - Scale( normal, Dot( normal, axis ) );
		var squared = Dot( projected, projected );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > 1e-6f ? Scale( projected, inverse ) : Vector3.Zero;
	}

	private static void UpdateResponse( PhysicalBoardForceState state, PhysicalBoardForceInput input, float gain )
	{
		state.SmoothedResponse = MultiplyAdd( 1f - gain, state.SmoothedResponse, gain * 0f );
		var mirrored = (input.MotionFlags & (1UL << 20)) != 0;
		var first = (input.ActionFlags & (1UL << (mirrored ? 58 : 59))) != 0;
		var second = (input.ActionFlags & (1UL << (mirrored ? 59 : 58))) != 0;
		(state.FirstTimer, state.FirstResponse) = Response( first, state.FirstTimer, state.FirstResponse, state.SmoothedResponse );
		(state.SecondTimer, state.SecondResponse) = Response( second, state.SecondTimer, state.SecondResponse, state.SmoothedResponse );
	}

	private static (float Timer, float Value) Response( bool active, float timer, float previous, float value )
	{
		if ( !active )
			return (0f, timer == 0f ? previous * .983f : previous);
		if ( timer > .165f )
			return (timer, value);
		timer += 1f / 60f;
		var alpha = Math.Clamp( timer * 6.060606f, 0f, 1f );
		return (timer, MultiplyAdd( value, alpha, (1f - alpha) * previous ));
	}
}
