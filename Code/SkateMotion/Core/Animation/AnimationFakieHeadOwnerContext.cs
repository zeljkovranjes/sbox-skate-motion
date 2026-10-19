using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Connects retained head behavior to keyed channel requests and the owner's parameter receiver.</summary>
internal sealed class AnimationFakieHeadOwnerContext : IAnimationFakieHeadContext
{
	private readonly AnimationOverlayOwner _channels;
	private readonly Func<InputCommandKey, int> _motion;
	private readonly IAnimationEntryActionContext _parameters;
	private readonly Func<bool> _primary;
	private readonly Func<bool> _secondary;
	private readonly Func<bool> _fakie;

	internal AnimationFakieHeadOwnerContext( AnimationOverlayOwner channels,
		Func<InputCommandKey, int> motion, IAnimationEntryActionContext parameters,
		Func<bool> primary, Func<bool> secondary, Func<bool> fakie )
	{
		ArgumentNullException.ThrowIfNull( channels );
		ArgumentNullException.ThrowIfNull( motion );
		ArgumentNullException.ThrowIfNull( parameters );
		ArgumentNullException.ThrowIfNull( primary );
		ArgumentNullException.ThrowIfNull( secondary );
		ArgumentNullException.ThrowIfNull( fakie );
		_channels = channels;
		_motion = motion;
		_parameters = parameters;
		_primary = primary;
		_secondary = secondary;
		_fakie = fakie;
	}

	public bool IsPrimaryInputActive() => _primary();
	public bool IsSecondaryInputActive() => _secondary();
	public bool IsRidingFakie() => _fakie();

	public void StartChannel( InputCommandKey channel, InputCommandKey motion )
	{
		var request = new AnimationChannelReplacement( _motion( motion ), 1f, false, true,
			.1f, .3f, .3f, 0x10000000, true );
		_channels.Request( channel, 0, true, request );
	}

	public void StopChannel( InputCommandKey channel ) => _channels.Stop( channel );
	public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner ) =>
		_parameters.SetParameter( name, value, normalized, owner );
}
