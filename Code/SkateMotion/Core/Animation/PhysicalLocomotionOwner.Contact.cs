namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using A = PhysicalCadenceArithmetic;
using O = PhysicalLocomotionOwnerArithmetic;

internal static partial class PhysicalLocomotionOwner
{
	private static void UpdateContact( PhysicalLocomotionOwnerState state, in PhysicalLocomotionOwnerInput input )
	{
		var previous = state.Contact;
		var same = previous.Identity == input.ContactIdentity;
		if ( !same && state.ReferenceActive )
		{
			state.Movement = O.Add( state.Movement, previous.Reference );
			state.MovementLength = A.Length( state.Movement );
		}
		state.Contact = PhysicalLocomotionContactReference.Update( previous, input.ContactFrame,
			input.ContactIdentity, true, Position( state.Frame ) );
		var history = state.AngularHistory;
		if ( same )
		{
			var old = previous.Frame;
			var current = input.ContactFrame;
			Vector3 Combine( Vector3 factors ) => O.AddScaled( Row2( current ), factors.Z,
				O.AddScaled( Row1( current ), factors.Y, O.Scale( Row0( current ), factors.X ) ) );
			var first = Combine( new( old.M11, old.M21, old.M31 ) );
			var second = Combine( new( old.M12, old.M22, old.M32 ) );
			var third = Combine( new( old.M13, old.M23, old.M33 ) );
			var forward = Row2( state.Frame );
			var transformed = O.AddScaled( third, forward.Z,
				O.AddScaled( second, forward.Y, O.Scale( first, forward.X ) ) );
			var horizontal = A.Reject( transformed, Vector3.UnitY );
			var normalized = O.Normalize( horizontal );
			if ( !A.Greater( A.Length( horizontal ), 0f ) )
				normalized = Vector3.Zero;
			var sine = O.Clamp( A.Dot( O.Cross( forward, normalized ), Vector3.UnitY ), -1f, 1f );
			var rate = A.Multiply( A.Divide( 1f, A.Value( 0x3c888888 ) ), O.ArcSine( sine ) );
			state.AngularHistory = new( rate, rate, A.Subtract( A.Add( rate, rate ), history.Y ), history.W );
			if ( !state.ReferenceActive && A.Greater( A.Dot( state.Contact.Reference, state.Contact.Reference ), A.Value( 0x3a83126e ) ) )
			{
				state.Movement = A.Subtract( state.Movement, state.Contact.Reference );
				state.MovementLength = A.Length( state.Movement );
				state.ReferenceActive = true;
			}
		}
		else
		{
			state.AngularHistory = new( 0f, 0f, 0f, history.W );
			state.ReferenceActive = false;
		}
		UpdateFeedback( state, input.FlipAcceleration );
	}

	private static void UpdateFeedback( PhysicalLocomotionOwnerState state, bool flip )
	{
		var solved = state.Solved;
		var acceleration = state.Contact.Acceleration;
		var local = new Vector3( A.TransformRow( solved.FrameRow0, acceleration ), 0f,
			A.TransformRow( solved.FrameRow2, acceleration ) );
		if ( flip )
			local = new( A.Multiply( local.X, -1f ), local.Y, A.Multiply( local.Z, -1f ) );
		local = O.Scale( local, A.Value( 0x3ccccccc ) );
		var length = A.Length( local );
		if ( A.Greater( length, 1f ) )
			local = O.Scale( local, A.Divide( 1f, length ) );
		state.Feedback = O.AddScaled( local, A.Value( 0x3d23d70a ),
			O.Scale( state.Feedback, A.Value( 0x3f75c28f ) ) );
		var velocity = state.Contact.Velocity;
		var strength = O.Clamp( A.Multiply( A.Length( new( velocity.X, 0f, velocity.Z ) ), A.Value( 0x3d4ccccc ) ), 0f, 1f );
		var angular = state.AngularHistory;
		state.AngularHistory = new( angular.X, angular.Y, angular.Z, strength );
	}

	private static void UpdateSecondary( PhysicalLocomotionOwnerState state, Vector3 displacement )
	{
		state.SecondaryFrame = state.Frame;
		if ( !state.SecondaryCorrectionActive )
			return;
		var correction = state.SecondaryCorrection;
		var length = A.Length( correction );
		if ( !A.Greater( length, A.Value( 0x3c23d70a ) ) )
		{
			state.SecondaryCorrection = Vector3.Zero;
			state.SecondaryCorrectionActive = false;
		}
		else
		{
			var direction = O.Scale( correction, A.Divide( 1f, length ) );
			var bound = A.Multiply( length, A.Value( 0xbd4ccccc ) );
			var minimum = A.Value( 0xbba3d70a );
			if ( A.Negative( A.Subtract( minimum, bound ) ) )
				bound = minimum;
			var projection = A.Dot( displacement, direction );
			var amount = A.Negative( A.Subtract( projection, bound ) ) ? projection : bound;
			if ( !A.Greater( amount, -length ) )
			{
				state.SecondaryCorrection = Vector3.Zero;
				state.SecondaryCorrectionActive = false;
			}
			else
				state.SecondaryCorrection = O.AddScaled( direction, amount, correction );
		}
		var secondary = state.SecondaryFrame;
		secondary.Translation = A.Subtract( Position( secondary ), state.SecondaryCorrection );
		state.SecondaryFrame = secondary;
	}
}
