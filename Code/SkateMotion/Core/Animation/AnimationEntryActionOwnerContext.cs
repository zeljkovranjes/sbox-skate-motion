namespace SkateMotion.Core.Animation;

/// <summary>Connects entry actions to the evaluated cache, actor tables and queued owner parameters.</summary>
internal sealed class AnimationEntryActionOwnerContext : IAnimationEntryActionContext
{
	private readonly AnimationEvaluatedAttributeCache _attributes;
	private readonly AnimationAttributeOverrideBuffer _parameters;
	private readonly Func<uint, AnimationActionValueTable> _tables;

	internal AnimationEntryActionOwnerContext( AnimationEvaluatedAttributeCache attributes,
		AnimationAttributeOverrideBuffer parameters, Func<uint, AnimationActionValueTable> tables,
		AnimationAttributeKey emptyTargetKey )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( parameters );
		ArgumentNullException.ThrowIfNull( tables );
		_attributes = attributes;
		_parameters = parameters;
		_tables = tables;
		EmptyTargetKey = emptyTargetKey;
	}

	/// <summary>The explicitly bound identity used for an omitted action target.</summary>
	public AnimationAttributeKey EmptyTargetKey { get; }

	/// <summary>Reads the retained evaluated records without querying the live tree.</summary>
	public bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result ) =>
		_attributes.TryGet( name, ref result );

	/// <summary>Resolves the current actor table at the action's lookup point.</summary>
	public AnimationActionValueTable GetValueTable( uint kind ) => _tables( kind );

	/// <summary>Queues the owner parameter without implicitly flushing it.</summary>
	public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner ) =>
		_parameters.WriteScalar( name, value, normalized, owner );
}
