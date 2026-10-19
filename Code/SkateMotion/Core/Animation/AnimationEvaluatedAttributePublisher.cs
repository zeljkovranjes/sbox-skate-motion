namespace SkateMotion.Core.Animation;

/// <summary>Clears and rebuilds the evaluated cache at the caller's post-advance phase.</summary>
internal sealed class AnimationEvaluatedAttributePublisher
{
	private readonly AnimationEvaluatedAttributeCache _cache;
	private readonly AnimationEvaluatedAttributeContext _context;
	private readonly AnimationEvaluatedAttributeBuffer _output;

	internal AnimationEvaluatedAttributePublisher( AnimationEvaluatedAttributeCache cache,
		int temporaryLists, int recordCapacity )
	{
		ArgumentNullException.ThrowIfNull( cache );
		_cache = cache;
		_context = new AnimationEvaluatedAttributeContext( temporaryLists, recordCapacity );
		_output = new AnimationEvaluatedAttributeBuffer( recordCapacity );
	}

	internal void Publish( IAnimationPosePlaybackNode root )
	{
		_cache.Publish( ReadOnlySpan<AnimationAttributeQueryResult>.Empty );
		_output.Clear();
		if ( root is null )
			return;
		AnimationEvaluatedAttributeContext.Require( root ).SampleEvaluatedAttributes( _context, _output, 15 );
		_cache.Publish( _output.Values );
	}
}
