using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Joins gesture dispatch to the shared overlay owner and explicit current actor providers.</summary>
internal sealed class AnimationCharacterGestureOwnerContext : IAnimationCharacterGestureContext
{
	private readonly InputRequestBuffer _requests;
	private readonly AnimationOverlayOwner _channels;
	private readonly AnimationGestureMotionTable _motions;
	private readonly Func<InputCommandKey, int> _resolveMotion;
	private readonly AnimationAttributeOverrideBuffer _attributes;
	private readonly Func<AnimationGestureSelection> _selection;
	private readonly Func<AnimationGesturePhysicalInput> _physical;
	private readonly AnimationGestureActorState _actor;
	private readonly Func<bool> _blocked;

	internal AnimationCharacterGestureOwnerContext( InputRequestBuffer requests, AnimationOverlayOwner channels,
		AnimationGestureMotionTable motions, Func<InputCommandKey, int> resolveMotion,
		AnimationAttributeOverrideBuffer attributes, Func<AnimationGestureSelection> selection,
		Func<AnimationGesturePhysicalInput> physical, Func<bool> blocked, AnimationGestureActorState actor )
	{
		_requests = requests ?? throw new ArgumentNullException( nameof( requests ) );
		_channels = channels ?? throw new ArgumentNullException( nameof( channels ) );
		_motions = motions ?? throw new ArgumentNullException( nameof( motions ) );
		_resolveMotion = resolveMotion ?? throw new ArgumentNullException( nameof( resolveMotion ) );
		_attributes = attributes ?? throw new ArgumentNullException( nameof( attributes ) );
		_selection = selection ?? throw new ArgumentNullException( nameof( selection ) );
		_physical = physical ?? throw new ArgumentNullException( nameof( physical ) );
		_blocked = blocked ?? throw new ArgumentNullException( nameof( blocked ) );
		_actor = actor ?? throw new ArgumentNullException( nameof( actor ) );
	}

	public bool ContainsRequest( InputCommandKey key )
	{
		for ( var index = 0; index < _requests.Count; index++ )
			if ( _requests[ index ].Key == key )
				return true;
		return false;
	}

	public bool HasChannel( InputCommandKey channel ) => _channels.Contains( channel );
	public void StopChannel( InputCommandKey channel ) => _channels.Stop( channel );
	public AnimationGestureSelection ReadSelection() => _selection();
	public AnimationGestureModeInput ReadModeInput()
	{
		var physical = _physical();
		return new( physical.Mode, physical.Gate, physical.HoldingBoard, _actor.FirstHand, _actor.SecondHand );
	}
	public bool IsUpBlocked() => _blocked();
	public float Remaining( InputCommandKey channel ) => _channels.Remaining( channel );
	public bool HasPending( InputCommandKey channel ) => _channels.HasPending( channel );
	public InputCommandKey Motion( int configuration, int mode, int phase ) => _motions.Build( configuration, mode, phase );

	public void StartChannel( InputCommandKey channel, InputCommandKey motion ) =>
		_channels.Request( channel, 0, true, new( _resolveMotion( motion ), 1f, false, true,
			.2f, .1f, .1f, 0xc0000000, true ) );

	public void QueueChannel( InputCommandKey channel, InputCommandKey motion, bool cycle ) =>
		_channels.Request( channel, 0, true, new( _resolveMotion( motion ), 1f, false, false,
			0f, .1f, .1f, cycle ? 0x50000000u : 0x40000000u, true ) );

	public void PublishGesture( int configuration, bool down ) => _actor.Publish( configuration, down );
	public float ReadDistance()
	{
		var physical = _physical();
		return physical.ForceUnitDistance ? 1f : physical.Distance;
	}
	public void SetDistance( AnimationAttributeKey key, float value ) => _attributes.WriteScalar( key, value, false, -1 );
}
