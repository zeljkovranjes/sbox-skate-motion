namespace SkateMotion.Core.Physics;

/// <summary>Decides board ownership from physical state, evaluated markers and retrieval completion.</summary>
internal static class PhysicalBoardController
{
	internal static PhysicalBoardDecision Evaluate( in PhysicalBoardInput input )
	{
		var mounting = ( input.RigFlags & ( 1UL << 51 ) ) != 0;
		var retrieve = ( input.ActionFlags & 0x800UL ) != 0;
		switch ( input.State )
		{
			case 1:
				var release = ( input.ActionFlags & 0x2000UL ) != 0;
				var allowContactRelease = !mounting && ( input.TimingFlags & ( 1UL << 60 ) ) == 0;
				return release || ( allowContactRelease && input.BoardContact )
					? new( 2, ChangeState: true )
					: new( 1, UpdateHeld: true );
			case 2:
				if ( mounting || retrieve )
					return new( 4, ChangeState: true, QueryGround: true );
				return input.GroundCheck || input.BodyMode == 6
					? new( 3, ChangeState: true, QueryGround: true )
					: new( 2, QueryGround: true );
			case 3:
				return mounting || retrieve ? new( 4, ChangeState: true ) : new( 3 );
			case 4:
				if ( !mounting && input.SelectedJointContact )
					return new( 2, ChangeState: true );
				// Keep the ordered less-than test: unordered progress also follows completion.
				return input.RetrievalProgress < 1f ? new( 4 )
					: new( 1, ChangeState: true, ClearVelocity: true );
			case 5:
				return input.BoardContact ? new( 2, ChangeState: true )
					: new( 5, UpdateContactSpring: true, ContactSpringStrength: 0.95f );
			default:
				return new( input.State );
		}
	}
}
