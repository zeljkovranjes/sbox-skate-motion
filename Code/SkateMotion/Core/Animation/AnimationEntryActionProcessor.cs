namespace SkateMotion.Core.Animation;

/// <summary>Applies ordered sampled and table-backed parameter actions with explicit fallbacks.</summary>
internal static class AnimationEntryActionProcessor
{
	internal static void Execute( ReadOnlySpan<AnimationEntryAction> actions, bool entering,
		IAnimationEntryActionContext context )
	{
		ArgumentNullException.ThrowIfNull( context );
		foreach ( var action in actions )
		{
			if ( action.Kind == 0 )
			{
				if ( entering )
					Sample( action, context );
			}
			else if ( action.Kind is 1 or 2 )
			{
				Lookup( action, context );
			}
		}
	}

	private static void Sample( AnimationEntryAction action, IAnimationEntryActionContext context )
	{
		var target = action.Target == context.EmptyTargetKey ? action.Attribute : action.Target;
		var sample = new AnimationAttributeQueryResult( default, default, -1f, -1f, 6, 0 );
		if ( context.TryGetAttribute( action.Attribute, ref sample ) )
		{
			if ( sample.Kind is 0 or 2 )
				context.SetParameter( target, sample.Payload.Scalar, false, -1 );
		}
		else if ( (action.Flags & 0x80000000u) != 0 )
		{
			context.SetParameter( target, action.Fallback, false, -1 );
		}
	}

	private static void Lookup( AnimationEntryAction action, IAnimationEntryActionContext context )
	{
		var key = action.Source;
		var target = action.Target == context.EmptyTargetKey
			? new AnimationAttributeKey( key.A, key.B, key.C, key.D, key.E ) : action.Target;
		var table = context.GetValueTable( action.Kind );
		if ( table is null )
			throw new InvalidOperationException( "A table-backed action requires an explicit actor value table." );
		var normalized = (action.Flags & 0x40000000u) != 0;
		if ( table.TryGet( key, out var value ) )
		{
			context.SetParameter( target, value, normalized, -1 );
		}
		else if ( (action.Flags & 0x80000000u) != 0 )
		{
			context.SetParameter( target, action.Fallback, action.Kind == 1 && normalized, -1 );
		}
	}
}
