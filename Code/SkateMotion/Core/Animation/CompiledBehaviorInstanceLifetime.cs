namespace SkateMotion.Core.Animation;

/// <summary>Identifies one family's instance handles and reference ownership.</summary>
internal sealed class CompiledBehaviorInstanceLifetime
{
	internal Action<int> Retain { get; }
	internal Action<int> Release { get; }

	internal CompiledBehaviorInstanceLifetime( Action<int> retain, Action<int> release )
	{
		ArgumentNullException.ThrowIfNull( retain );
		ArgumentNullException.ThrowIfNull( release );
		Retain = retain;
		Release = release;
	}
}
