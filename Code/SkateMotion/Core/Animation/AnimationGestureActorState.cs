namespace SkateMotion.Core.Animation;

/// <summary>Shares hand occupancy and the most recently published gesture between graph behaviors.</summary>
internal sealed class AnimationGestureActorState
{
	internal uint FirstHand { get; private set; }
	internal uint SecondHand { get; private set; }
	internal int PublishedGesture { get; private set; }
	internal bool GesturePublished { get; private set; }
	internal bool PublishedDown { get; private set; }

	internal AnimationGestureActorState( uint firstHand, uint secondHand,
		int publishedGesture, bool gesturePublished, bool publishedDown )
	{
		FirstHand = firstHand;
		SecondHand = secondHand;
		PublishedGesture = publishedGesture;
		GesturePublished = gesturePublished;
		PublishedDown = publishedDown;
	}

	internal void Publish( int configuration, bool down )
	{
		PublishedDown = down;
		PublishedGesture = configuration;
		GesturePublished = true;
	}

	internal void AdjustHand( int side, int change )
	{
		if ( side == 0 )
			FirstHand = unchecked( FirstHand + (uint)change );
		else if ( side == 1 )
			SecondHand = unchecked( SecondHand + (uint)change );
	}
}
