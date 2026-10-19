namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Publishes body-derived board input before character and motor processing.</summary>
internal static class PhysicalBoardInputPublisher
{
	internal static void Update( PhysicalBoardInputState state, PhysicalBodyBinding[] bodies,
		int bodyCount, ulong motionFlags, float signedSpeed, Vector3 targetHeading )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( bodies );
		if ( bodies.Length < 7 || bodyCount < 0 || bodyCount > bodies.Length )
			throw new ArgumentOutOfRangeException( nameof( bodyCount ) );
		for ( var i = 0; i < bodyCount; i++ )
		{
			if ( bodies[ i ]?.Body is null )
				throw new ArgumentException( "Mass accumulation requires physical bodies.", nameof( bodies ) );
		}
		ArgumentNullException.ThrowIfNull( bodies[ 6 ] );

		var mass = 0f;
		for ( var i = 0; i < bodyCount; i++ )
			mass += 1f / bodies[ i ].Body.InverseMass;
		state.Mass = mass;
		state.PhysicalFrame = bodies[ 6 ].Read();
		var physical = state.PhysicalFrame;
		var mirrored = (motionFlags & (1UL << 20)) != 0;
		var stanceRotation = mirrored
			? new LimbFrame( -physical.Rotation.X, physical.Rotation.Y, -physical.Rotation.Z )
			: physical.Rotation;
		state.StanceFrame = new LimbRigidTransform( stanceRotation, physical.Translation );
		state.InverseStanceFrame = state.StanceFrame.RigidInverse();

		state.StanceSign = mirrored ? -1f : 1f;
		state.StanceAxes = new LimbFrame( Scale( physical.Rotation.X, state.StanceSign ),
			physical.Rotation.Y, Scale( physical.Rotation.Z, state.StanceSign ) );
		var horizontal = state.StanceAxes.Z with { Y = 0f };
		if ( Dot( horizontal, horizontal ) > 1e-5f )
			horizontal = Normalize( horizontal );
		state.HorizontalDirection = horizontal;
		var projection = Scale( horizontal, Dot( state.StanceAxes.Y, horizontal ) );
		state.PerpendicularDirection = Normalize( state.StanceAxes.Y - projection );
		state.SignedVelocityDirection = Scale( state.StanceAxes.Z, signedSpeed );
		var travelSign = 0f - signedSpeed >= 0f ? -1f : 1f;
		state.Speed = travelSign * signedSpeed;
		state.TravelDirection = Scale( state.StanceAxes.Z, travelSign );
		state.Heading = UpdateHeading( state.Heading, targetHeading );
	}

	private static Vector3 UpdateHeading( Vector3 previous, Vector3 target )
	{
		if ( Dot( previous, target ) > .935f )
			return target;
		var blended = AddScaled( previous, .9f, Scale( target, .1f ) );
		var squared = Dot( blended, blended );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > 1e-6f ? Scale( blended, inverse ) : Vector3.Zero;
	}
}
