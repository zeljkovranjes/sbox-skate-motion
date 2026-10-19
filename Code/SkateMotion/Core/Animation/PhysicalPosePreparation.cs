namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Prepares physical target frames and feedback before the rig and joint-output stages.</summary>
internal static class PhysicalPosePreparation
{
	internal static byte Update( PhysicalPosePreparationState state, PhysicalBodyBinding[] targets,
		PhysicalBodyBinding[] feedback )
	{
		if ( targets.Length != 4 || feedback.Length != 2 || feedback[ 0 ].Body is null || feedback[ 1 ].Body is null )
			throw new ArgumentException( "Preparation requires four target bindings and two bound feedback bodies." );

		var desired = state.ModelToWorld.Compose( state.SolverFrame );
		state.ComposedContact = state.ContactFrame.Compose( state.ModelToWorld.Compose( state.ModelOffset ) );
		var difference = targets[ 0 ].Read().Translation - desired.Translation;
		targets[ 0 ].Write( desired with { Rotation = desired.Rotation.Orthonormalized() } );
		targets[ 1 ].Write( state.SecondaryTarget with { Rotation = state.SecondaryTarget.Rotation.Orthonormalized() } );
		targets[ 2 ].Write( state.FirstFeedbackFrame );
		targets[ 3 ].Write( state.SecondFeedbackFrame );

		state.CachedPosition = state.SecondFeedbackFrame.Translation;
		state.FirstFeedbackPosition = state.FirstFeedbackFrame.Translation;
		state.SecondFeedbackPosition = state.SecondFeedbackFrame.Translation;
		feedback[ 0 ].Body.Velocity = Feedback( state.FirstFeedbackPosition, feedback[ 0 ].Read().Translation );
		feedback[ 1 ].Body.Velocity = Feedback( state.SecondFeedbackPosition, feedback[ 1 ].Read().Translation );

		// The distance/suppression result follows the physical writes; it does not guard them.
		return Dot( difference, difference ) > 1f ? (byte)0 : (byte)(state.SuppressionFlag ^ 1);
	}

	private static Vector3 Feedback( Vector3 authored, Vector3 physical )
	{
		var velocity = Scale( authored - physical, 60f );
		var length = SquareRoot( Dot( velocity, velocity ) );
		if ( length >= 1.52587890625e-5f )
			velocity = Scale( Scale( velocity, MathF.Min( length, 40f ) ), Reciprocal( length ) );
		return velocity;
	}
}
