namespace SkateMotion.Core.Input;

internal static class QueuedInputProcessor
{
	private static readonly InputCommandKey[] FingerCommands =
	[
		InputCommandKey.Encode( "FINGERFLIP"u8 ), InputCommandKey.Encode( "FS_VARIAL"u8 ),
		InputCommandKey.Encode( "BS_VARIAL"u8 )
	];

	private static readonly InputCommandKey[] LateCommands =
	[
		InputCommandKey.Encode( "L_F_KICKFLIP"u8 ), InputCommandKey.Encode( "L_B_KICKFLIP"u8 ),
		InputCommandKey.Encode( "L_F_HEELFLIP"u8 ), InputCommandKey.Encode( "L_B_HEELFLIP"u8 ),
		InputCommandKey.Encode( "L_FS_SHUVIT"u8 ), InputCommandKey.Encode( "L_BS_SHUVIT"u8 )
	];

	private static readonly InputCommandKey[] Flips =
	[
		InputCommandKey.Encode( "FRONTFLIP"u8 ), InputCommandKey.Encode( "BACKFLIP"u8 )
	];

	private static readonly InputCommandKey[] Slides =
	[
		InputCommandKey.Encode( "SLIDEFS180"u8 ), InputCommandKey.Encode( "SLIDEBS180"u8 )
	];

	private static readonly InputCommandKey[] BasicTricks =
	[
		InputCommandKey.Encode( "OLLIE"u8 ), InputCommandKey.Encode( "KICKFLIP"u8 ),
		InputCommandKey.Encode( "HEELFLIP"u8 ), InputCommandKey.Encode( "HARDFLIP"u8 ),
		InputCommandKey.Encode( "INWARDHEELFLIP"u8 ), InputCommandKey.Encode( "VARIALKICKFLIP"u8 ),
		InputCommandKey.Encode( "VARIALHEELFLIP"u8 ), InputCommandKey.Encode( "POPSHUVIT"u8 ),
		InputCommandKey.Encode( "FSPOPSHUVIT"u8 ), InputCommandKey.Encode( "360POPSHUVIT"u8 ),
		InputCommandKey.Encode( "FS360POPSHUVIT"u8 )
	];

	internal static QueuedInputActions Evaluate( QueuedInputCommand command, QueuedInputContext context )
	{
		if ( command.Kind == 2 )
			return new( true, false, command.Key, 0f );
		if ( command.Kind != 3 )
			return default;
		var key = command.Key;
		if ( context.SuppressFingerCommands && Contains( FingerCommands, key ) )
			return default;
		if ( context.SuppressLateCommands && (context.BlockAllDuringLateRestriction || Contains( LateCommands, key )) )
			return default;
		if ( Contains( Flips, key ) && (!context.HasAirGrab || context.Pushing || context.SuppressFlips) )
			return default;
		if ( context.SuppressSlides && Contains( Slides, key ) )
			return default;
		if ( context.RestrictToBasicTricks && !Contains( BasicTricks, key ) )
			return default;
		return new( true, true, key, command.Speed );
	}

	internal static bool ConsumeHoldPattern( ref bool pending )
	{
		var requested = pending;
		pending = false;
		return requested;
	}

	private static bool Contains( ReadOnlySpan<InputCommandKey> values, InputCommandKey key )
	{
		foreach ( var value in values )
		{
			if ( value == key )
				return true;
		}
		return false;
	}
}
