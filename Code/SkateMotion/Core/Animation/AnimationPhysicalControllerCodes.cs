namespace SkateMotion.Core.Animation;

/// <summary>Publishes current and parent controller codes from the physical controller owner.</summary>
internal readonly record struct AnimationPhysicalControllerCodes( int Current, int Parent )
{
	internal static AnimationPhysicalControllerCodes Capture( Func<int> readCurrent )
	{
		ArgumentNullException.ThrowIfNull( readCurrent );
		var current = readCurrent();
		return new( current, ParentOf( readCurrent() ) );
	}

	internal static int ParentOf( int current )
	{
		if ( current > 699 ) return 700;
		if ( current > 599 ) return 600;
		if ( current > 499 ) return 500;
		if ( current > 399 ) return 400;
		if ( current > 299 ) return 300;
		if ( current > 199 ) return 200;
		return current > 99 ? 100 : 0;
	}
}
