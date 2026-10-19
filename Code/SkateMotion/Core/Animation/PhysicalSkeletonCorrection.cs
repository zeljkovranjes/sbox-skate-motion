namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Adds a mass-scaled correction to the main skeleton's linear accumulators.</summary>
internal static class PhysicalSkeletonCorrection
{
	private const float TimeStep = .01666666753590107f;

	internal static void Apply( PhysicalBodyBinding[] bodies, Vector3 correction )
	{
		if ( bodies.Length <= 2 )
			throw new ArgumentException( "Correction requires skeleton bodies and two trailing targets." );
		for ( var i = 0; i < bodies.Length - 2; i++ )
		{
			if ( bodies[ i ].Body is null )
				throw new ArgumentException( "Every corrected skeleton body must be bound." );
		}

		var perStep = Scale( correction, Reciprocal( TimeStep ) );
		for ( var i = 0; i < bodies.Length - 2; i++ )
		{
			var body = bodies[ i ].Body;
			body.LinearAccumulator += Scale( perStep, body.InverseMass );
			body.AccumulatorState = 0;
		}
	}
}
