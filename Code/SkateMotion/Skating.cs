using Sandbox;
using SkateMotion.Components;
using SkateMotion.Resources;

namespace SkateMotion;

/// <summary>Creates an original skateboard with native deck collision and independent wheel contacts.</summary>
public static class Skating
{
	/// <summary>Creates a board at a world transform; supply controls through the returned component.</summary>
	public static Skateboard Create( Scene scene, Transform transform, SkateProfile profile = null )
	{
		ArgumentNullException.ThrowIfNull( scene );
		var board = new GameObject( scene, false, "Skateboard" );
		board.WorldTransform = transform;
		board.Components.Create<Rigidbody>();
		var controller = board.Components.Create<Skateboard>();
		controller.Profile = profile;
		board.Enabled = true;
		return controller;
	}

	/// <summary>Creates a grindable rail through world points; it needs no collider, but boardslides also use one if present.</summary>
	public static GrindRail CreateRail( Scene scene, params Vector3[] points )
	{
		ArgumentNullException.ThrowIfNull( scene );
		ArgumentNullException.ThrowIfNull( points );
		if ( points.Length < 2 )
			throw new ArgumentException( "A rail needs at least two points.", nameof( points ) );
		var rail = new GameObject( scene, true, "Grind rail" );
		rail.WorldPosition = points[ 0 ];
		var component = rail.Components.Create<GrindRail>();
		foreach ( var point in points )
			component.Points.Add( point - points[ 0 ] );
		return component;
	}
}
