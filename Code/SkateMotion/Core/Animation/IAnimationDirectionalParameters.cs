namespace SkateMotion.Core.Animation;

/// <summary>Child parameter updates and scalar samples for a directional selection pass.</summary>
internal interface IAnimationDirectionalParameters
{
	/// <summary>Updates one child's parameters and reports whether its selection changed.</summary>
	bool UpdateChild( int child );

	/// <summary>Returns the child's current sample, or zero when its scalar query is absent.</summary>
	float QuerySample( int child );
}
