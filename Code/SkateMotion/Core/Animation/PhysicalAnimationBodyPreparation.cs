namespace SkateMotion.Core.Animation;

/// <summary>Moves requested rider bodies between world partitions before consuming animation inputs.</summary>
internal static class PhysicalAnimationBodyPreparation
{
	internal static void Update( PhysicalBodyBinding[] bodies, int count )
	{
		ArgumentNullException.ThrowIfNull( bodies );
		if ( count < 0 || count > bodies.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		for ( var i = 0; i < count; i++ )
		{
			var body = bodies[ i ].Body;
			if ( (body.MotionMode & 7) == 2 )
				MoveToModeFour( body );
			body.AccumulatorState = 0;
		}
	}

	private static void MoveToModeFour( PhysicalBodyState body )
	{
		var world = body.Partitions;
		var node = body.QueueLink;
		var previous = node.Previous;
		var destination = world.ModeFourHead;
		previous.Next = node.Next;
		var first = destination.Next;
		var next = node.Next;
		node.Next = first;
		var oldFirst = destination.Next;
		body.MotionMode = (body.MotionMode & 8) | 4;
		destination.Next = node;
		next.Previous = previous;
		node.Previous = destination;
		oldFirst.Previous = node;
		body.AccumulatorState = unchecked( world.Epoch - 1 );
		world.ModeTwoCount = unchecked( world.ModeTwoCount - 1 );
		world.ModeFourCount = unchecked( world.ModeFourCount + 1 );
	}
}
