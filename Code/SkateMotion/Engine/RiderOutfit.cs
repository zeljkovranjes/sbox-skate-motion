using Sandbox;

namespace SkateMotion.Engine;

internal static class RiderOutfit
{
	internal static void ApplyDefault( SkinnedModelRenderer renderer )
	{
		var clothing = new ClothingContainer();
		Add( clothing, "models/citizen_clothes/shirt/tshirt/tshirt.clothing" );
		Add( clothing, "models/citizen_clothes/trousers/jeans/jeans.clothing" );
		Add( clothing, "models/citizen_clothes/shoes/sneakers/sneakers.clothing" );
		var dresser = renderer.Components.Create<Dresser>();
		dresser.BodyTarget = renderer;
		dresser.ApplyHeightScale = false;
		dresser.Apply( clothing );
	}

	private static void Add( ClothingContainer outfit, string path )
	{
		var item = ResourceLibrary.Get<Clothing>( path );
		if ( item is null )
		{
			Log.Warning( $"Skate Motion: default rider clothing is unavailable: {path}" );
			return;
		}
		outfit.Add( item );
	}
}
