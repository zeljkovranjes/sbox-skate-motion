namespace SkateMotion.Core.Constraints;

using Vector4 = System.Numerics.Vector4;

internal static class ContactImpulse
{
	// Lanes: normal, first tangent, second tangent, separation correction.
	internal static Vector4 Project( Vector4 previous, Vector4 candidate,
		float staticFriction, float slidingFriction )
	{
		return ProjectCore( previous, candidate, staticFriction, slidingFriction, true );
	}

	internal static Vector4 ProjectGenerated( Vector4 previous, Vector4 candidate,
		float staticFriction, float slidingFriction )
	{
		return ProjectCore( previous, candidate, staticFriction, slidingFriction, false );
	}

	private static Vector4 ProjectCore( Vector4 previous, Vector4 candidate,
		float staticFriction, float slidingFriction, bool requireOrderedFriction )
	{
		if ( !Finite( previous ) || !Finite( candidate ) || previous.X < 0f
			|| !float.IsFinite( staticFriction ) || !float.IsFinite( slidingFriction )
			|| slidingFriction < 0f || staticFriction < 0f
			|| (requireOrderedFriction && staticFriction < slidingFriction) )
		{
			throw new ArgumentOutOfRangeException( nameof( candidate ),
				requireOrderedFriction
					? "Contact impulses must be finite, with nonnegative normal load and ordered friction coefficients."
					: "Generated contact impulses must be finite, with nonnegative normal load and friction coefficients." );
		}

		// Both tangent limits use the previous pass's normal impulse.
		var threshold = previous.X * staticFriction;
		var slidingLimit = previous.X * slidingFriction;
		return new Vector4( Math.Max( 0f, candidate.X ),
			ProjectTangent( candidate.Y, threshold, slidingLimit ),
			ProjectTangent( candidate.Z, threshold, slidingLimit ),
			Math.Max( 0f, candidate.W ) );
	}

	private static float ProjectTangent( float value, float threshold, float slidingLimit )
	{
		if ( value < -threshold )
			return -slidingLimit;
		if ( value > threshold )
			return slidingLimit;
		return value;
	}

	private static bool Finite( Vector4 value ) => float.IsFinite( value.X )
		&& float.IsFinite( value.Y ) && float.IsFinite( value.Z ) && float.IsFinite( value.W );
}
