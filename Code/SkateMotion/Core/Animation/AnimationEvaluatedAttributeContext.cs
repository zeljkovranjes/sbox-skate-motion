namespace SkateMotion.Core.Animation;

/// <summary>Provides bounded reusable scratch lists for nested attribute traversal.</summary>
internal sealed class AnimationEvaluatedAttributeContext
{
	private readonly AnimationEvaluatedAttributeBuffer[] _buffers;
	private int _count;

	internal AnimationEvaluatedAttributeContext( int temporaryLists, int recordCapacity )
	{
		if ( temporaryLists < 0 )
			throw new ArgumentOutOfRangeException( nameof( temporaryLists ) );
		_buffers = new AnimationEvaluatedAttributeBuffer[ temporaryLists ];
		for ( var i = 0; i < _buffers.Length; i++ )
			_buffers[ i ] = new AnimationEvaluatedAttributeBuffer( recordCapacity );
	}

	internal AnimationEvaluatedAttributeBuffer Rent()
	{
		if ( _count == _buffers.Length )
			throw new InvalidOperationException( "Attribute traversal exceeds the supplied scratch depth." );
		var buffer = _buffers[ _count++ ];
		buffer.Clear();
		return buffer;
	}

	internal void Return( AnimationEvaluatedAttributeBuffer buffer )
	{
		if ( _count == 0 || !ReferenceEquals( _buffers[ _count - 1 ], buffer ) )
			throw new InvalidOperationException( "Attribute scratch lists must be returned in reverse acquisition order." );
		_count--;
	}

	internal static IAnimationEvaluatedAttributeNode Require( IAnimationPosePlaybackNode node ) =>
		node as IAnimationEvaluatedAttributeNode
			?? throw new NotSupportedException( "This retained node does not implement evaluated attribute traversal." );
}
