using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Retains continuation and catch requests for an active trick behavior.</summary>
internal sealed class TrickContinuationMonitor
{
	private const uint ContinuationFlag = 1u << 22;
	private const uint CatchFlag = 1u << 21;
	private readonly InputCommandKey[] _continuationRequests;
	private readonly InputCommandKey _catchRequest;

	internal int UpdateCount { get; set; }
	internal uint Flags { get; set; }
	internal bool ContinuationRequested => (Flags & ContinuationFlag) != 0;
	internal bool CatchRequested => (Flags & CatchFlag) != 0;

	internal TrickContinuationMonitor( InputCommandKey[] continuationRequests, InputCommandKey catchRequest )
	{
		ArgumentNullException.ThrowIfNull( continuationRequests );
		_continuationRequests = continuationRequests.AsSpan().ToArray();
		_catchRequest = catchRequest;
	}

	internal void Enter() => UpdateCount = 0;

	internal void Update( InputRequestBuffer requests )
	{
		ArgumentNullException.ThrowIfNull( requests );
		UpdateCount = unchecked(UpdateCount + 1);
		// Entry consumes the first update without inspecting this frame's requests.
		if ( UpdateCount <= 1 )
			return;
		for ( var index = 0; index < _continuationRequests.Length; index++ )
		{
			if ( Contains( requests, _continuationRequests[ index ] ) )
			{
				Flags |= ContinuationFlag;
				return;
			}
		}
		if ( Contains( requests, _catchRequest ) )
			Flags |= CatchFlag;
	}

	internal void Exit() => Flags &= ~(ContinuationFlag | CatchFlag);

	private static bool Contains( InputRequestBuffer requests, InputCommandKey key )
	{
		for ( var index = 0; index < requests.Count; index++ )
		{
			if ( requests[ index ].Key == key )
				return true;
		}
		return false;
	}
}
