namespace SkateMotion.Core.Animation;

/// <summary>Emits ordered evaluated records through a separate retained-clock traversal.</summary>
internal interface IAnimationEvaluatedAttributeNode
{
	/// <summary>Appends eligible records without advancing or consuming clock history.</summary>
	void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags );
}
