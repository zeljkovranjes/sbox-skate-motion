using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal sealed partial class AnimationOverlayOwner
{
	internal bool Contains( InputCommandKey channel ) => Find( channel ) is not null;
	internal bool HasPending( InputCommandKey channel ) => Find( channel )?.Owner.HasPending ?? false;

	internal float Remaining( InputCommandKey channel )
	{
		var entry = Find( channel );
		if ( entry is null )
			return 0f;
		var root = entry.Owner.Root;
		var remaining = root.Duration - root.Elapsed;
		return remaining >= 0f ? remaining : 0f;
	}
}
