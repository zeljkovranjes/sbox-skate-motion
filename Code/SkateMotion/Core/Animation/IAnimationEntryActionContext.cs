namespace SkateMotion.Core.Animation;

/// <summary>Provides the actor's current attribute query, value tables and parameter receiver.</summary>
internal interface IAnimationEntryActionContext
{
	AnimationAttributeKey EmptyTargetKey { get; }
	bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result );
	AnimationActionValueTable GetValueTable( uint kind );
	void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner );
}
