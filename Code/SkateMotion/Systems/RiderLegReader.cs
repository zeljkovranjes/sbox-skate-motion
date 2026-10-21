using Sandbox;
using SkateMotion.Components;
using SkateMotion.Engine;

namespace SkateMotion.Systems;

/// <summary>
/// Once a frame, after every update, reads the stock walk each rider's legs follow on foot (see
/// ClipRiderAnimation.LegSource): by then every skeleton is posed, where read during the rider's own update one
/// could be part way through.
/// </summary>
public sealed class RiderLegReader : GameObjectSystem<RiderLegReader>
{
	public RiderLegReader( Scene scene ) : base( scene ) => Listen( Stage.FinishUpdate, 0, Read, "RiderLegReader" );

	private void Read()
	{
		foreach ( var rider in Scene.GetAllComponents<SkateRider>() )
			rider.ReadLegs();
	}
}
