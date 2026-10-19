namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Drives board bodies from the seventh body's position error.</summary>
internal static class PhysicalBoardFeedback
{
	internal static void Update( PhysicalBodyBinding[] bodies, int bodyCount,
		Vector3 desiredPosition, float deltaTime, float gain )
	{
		ArgumentNullException.ThrowIfNull( bodies );
		if ( bodies.Length < 7 || bodyCount < 0 || bodyCount > bodies.Length )
			throw new ArgumentException( "Board feedback requires a seventh reference body and a valid body count." );
		// The reference is the physical position, without the binding's local offset.
		var difference = desiredPosition - bodies[ 6 ].Body.Pose.Translation;
		var velocity = LimbSolverMath.Scale( difference, gain / deltaTime );
		for ( var i = 0; i < bodyCount; i++ )
			bodies[ i ].Body.Velocity = velocity;
	}
}
