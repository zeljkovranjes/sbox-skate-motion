namespace SkateMotion.Core.Animation;

using A = PhysicalCadenceArithmetic;
using O = PhysicalLocomotionOwnerArithmetic;

internal static class PhysicalLocomotionPlacement
{
	/// <summary>Updates the retained placement after the physical owner has prepared and solved its frames.</summary>
	internal static PhysicalLocomotionPlacementState Update( in PhysicalLocomotionPlacementState state,
		PhysicalLocomotionOwnerState owner, in PhysicalLocomotionOwnerInput input,
		in PhysicalLocomotionMotionInput physical, in PhysicalLocomotionPlacementInput placement )
	{
		ArgumentNullException.ThrowIfNull( owner );
		var axis = physical.OrientationAxis;
		var basis = owner.SecondaryFrame.Translation;
		var projection = A.Dot( A.Subtract( input.Position, basis ), axis );
		if ( (input.ContactFlags & 1u) != 0 && A.Greater( projection, 0f ) )
			basis = input.Position;
		if ( owner.SecondaryCorrectionActive )
			basis = state.SecondaryTarget;
		if ( (input.ContactFlags & 2u) != 0 )
		{
			var secondary = A.Dot( A.Subtract( placement.SecondaryPosition, basis ), axis );
			var solved = A.Dot( A.Subtract( owner.Solved.Position, basis ), axis );
			var furthest = A.Negative( A.Subtract( secondary, solved ) ) ? solved : secondary;
			var limit = A.Value( 0x3e999999 );
			var distance = A.Negative( A.Subtract( furthest, limit ) ) ? furthest : limit;
			if ( A.Greater( distance, 0f ) )
				basis = O.AddScaled( axis, distance, basis );
		}
		var tangent = A.Reject( A.Subtract( placement.DesiredPosition, basis ), axis );
		var target = O.AddScaled( axis, A.Value( 0x3f733333 ), O.Add( basis, tangent ) );
		var delta = A.Subtract( target, state.Reference );
		var axialDelta = A.Dot( delta, axis );
		var bounded = O.Clamp( axialDelta, A.Value( 0xbdcccccc ), A.Value( 0x3dcccccc ) );
		var adjusted = O.AddScaled( axis, A.Subtract( bounded, axialDelta ), delta );
		return state with { Reference = O.Add( state.Reference, adjusted ) };
	}

	/// <summary>Binds completed physical outputs before the character wrapper adopts its new frame.</summary>
	internal static PhysicalOffboardCharacterInput ApplyToCharacterInput( PhysicalLocomotionOwnerState owner,
		in PhysicalLocomotionPlacementState placement, in PhysicalOffboardCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( owner );
		return input with { Solved = owner.Solved, FeedbackReference = placement.Reference };
	}
}
