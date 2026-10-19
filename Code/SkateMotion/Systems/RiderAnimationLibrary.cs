using System.Collections.Generic;
using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Systems;

/// <summary>Loads each clip set and graph once per scene so riders share them.</summary>
public sealed class RiderAnimationLibrary : GameObjectSystem<RiderAnimationLibrary>
{
	private readonly Dictionary<(string, string), RiderAnimationAssets> _loaded = new();

	/// <summary>Creates the per-scene cache.</summary>
	public RiderAnimationLibrary( Scene scene ) : base( scene ) { }

	internal RiderAnimationAssets Get( string clipsPath, string graphPath )
	{
		var key = (clipsPath ?? "", graphPath ?? "");
		if ( !_loaded.TryGetValue( key, out var assets ) )
		{
			assets = RiderAnimationAssets.Load( clipsPath, graphPath );
			_loaded[ key ] = assets;
		}
		return assets;
	}
}
