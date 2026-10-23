namespace SkateMotion.Core.Networking;

/// <summary>
/// A board's state and its rider's controls as the board's owner shares them with every other player. Only the owner
/// simulates the board; everyone else shows it, and the rider's tricks, grabs, grinds, slides and flips, from what it
/// shares: with only speed, wheels, air, bail, balance and grind shared, the controls were never seen, so no trick, grab
/// or flip played anywhere but on the rider's own screen. The yes/no parts travel packed as bits.
/// </summary>
internal static class NetBoard
{
	/// <summary>The yes/no parts of a board's state, a bit each.</summary>
	internal static int StateFlags( SkateState state ) =>
		Bit( state.IsAirborne, 0 ) | Bit( state.IsBailed, 1 ) | Bit( state.Reverting, 2 ) | Bit( state.Fakie, 3 )
		| Bit( state.WallRiding, 4 ) | Bit( state.HandPlanting, 5 ) | Bit( state.GrindRailOnRight, 6 )
		| Bit( state.GrindNoseRight, 7 ) | Bit( state.GrindBackward, 8 ) | Bit( state.GrindTwisted, 9 ) | Bit( state.GrindLow, 10 )
		| Bit( state.GrindOnLedgeTop, 11 ) | Bit( state.NamedBackward, 12 ) | Bit( state.NamedTwisted, 13 )
		| Bit( state.NamedLow, 14 ) | Bit( state.NamedOnLedgeTop, 15 );

	/// <summary>A board's state from its shared parts.</summary>
	internal static SkateState State( float speed, int wheels, float landingSpeed, float crouch, int powerSlide, int bodyFlip,
		SkateBalanceMode balance, SkateGrind grind, SkateGrind namedGrind, int flags ) =>
		new( speed, wheels, Has( flags, 0 ), Has( flags, 1 ), landingSpeed )
		{
			Balance = balance,
			Grind = grind,
			PowerSlide = powerSlide,
			Reverting = Has( flags, 2 ),
			Fakie = Has( flags, 3 ),
			BodyFlip = bodyFlip,
			WallRiding = Has( flags, 4 ),
			HandPlanting = Has( flags, 5 ),
			GrindRailOnRight = Has( flags, 6 ),
			GrindNoseRight = Has( flags, 7 ),
			GrindBackward = Has( flags, 8 ),
			GrindTwisted = Has( flags, 9 ),
			GrindLow = Has( flags, 10 ),
			GrindOnLedgeTop = Has( flags, 11 ),
			NamedGrind = namedGrind,
			NamedBackward = Has( flags, 12 ),
			NamedTwisted = Has( flags, 13 ),
			NamedLow = Has( flags, 14 ),
			NamedOnLedgeTop = Has( flags, 15 ),
			Crouch = crouch
		};

	/// <summary>The yes/no parts of the rider's controls, a bit each.</summary>
	internal static int InputFlags( SkateInput input ) =>
		Bit( input.Slide, 0 ) | Bit( input.Goofy, 1 ) | Bit( input.Pump, 2 ) | Bit( input.Gesture, 3 ) | Bit( input.NewPush, 4 )
		| Bit( input.HippyJump, 5 ) | Bit( input.Catch, 6 ) | Bit( input.HandPlant, 7 );

	/// <summary>The rider's controls from their shared parts.</summary>
	internal static SkateInput Input( float steer, float push, float brake, float flickX, float flickY, float lean,
		SkateAirGrab grab, SkatePushFeet pushFeet, SkatePushFeet feetOff, int flags ) =>
		new( steer, push, brake, flickX, flickY, lean, Has( flags, 0 ), Has( flags, 1 ), Has( flags, 2 ), grab, Has( flags, 3 ) )
		{
			PushFeet = pushFeet,
			NewPush = Has( flags, 4 ),
			HippyJump = Has( flags, 5 ),
			Catch = Has( flags, 6 ),
			FeetOff = feetOff,
			HandPlant = Has( flags, 7 )
		};

	private static int Bit( bool value, int bit ) => value ? 1 << bit : 0;

	private static bool Has( int flags, int bit ) => (flags & (1 << bit)) != 0;
}
