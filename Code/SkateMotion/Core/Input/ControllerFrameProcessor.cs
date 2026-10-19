namespace SkateMotion.Core.Input;

/// <summary>Owns frame clearing, input selection, queue consumption and forced requests.</summary>
internal sealed class ControllerFrameProcessor
{
	private static readonly InputCommandKey _forceBrake = InputCommandKey.Encode( "FORCEBRAKE"u8 );
	private static readonly InputCommandKey _forcedBail = InputCommandKey.Encode( "WIPEOUTREQUEST"u8 );
	private readonly ControllerActionRouter _router;
	internal bool BailPending { get; set; }
	internal InputRequestBuffer Requests => _router.Requests;

	internal ControllerFrameProcessor( ControllerActionRouter router )
	{
		ArgumentNullException.ThrowIfNull( router );
		_router = router;
	}

	internal void Update( ControllerReading reading, float delta, ControllerActionContext context,
		ControllerFrameContext frame, QueuedInputCommand[] commands, ref int commandCount,
		InputRequestBuffer overrideRequests )
	{
		ArgumentNullException.ThrowIfNull( commands );
		if ( commandCount < 0 || commandCount > commands.Length )
		{
			throw new ArgumentOutOfRangeException( nameof( commandCount ) );
		}
		if ( frame.HasOverride )
		{
			ArgumentNullException.ThrowIfNull( overrideRequests );
			if ( ReferenceEquals( overrideRequests, Requests ) )
			{
				throw new ArgumentException( "Override requests must have independent storage.", nameof( overrideRequests ) );
			}
		}

		Requests.Clear();
		if ( frame.HasOverride )
		{
			for ( var i = 0; i < overrideRequests.Count; i++ )
			{
				var request = overrideRequests[ i ];
				Requests.Add( request.Key, request.Value );
			}
		}
		else if ( !frame.ForceBrake || !frame.BlockControllerDuringForcedBrake )
		{
			_router.Update( reading, delta, context, commands, commandCount );
		}

		// Even a skipped controller update consumes this frame's queued commands.
		commandCount = 0;
		if ( BailPending && !frame.SuppressForcedBail )
		{
			Requests.Set( _forcedBail, 1f );
		}
		BailPending = false;
		if ( frame.ForceBrake )
		{
			Requests.Set( _forceBrake, 1f );
		}
	}
}
