using System.Linq;
using Sandbox;

namespace SkateMotion.Systems;

/// <summary>
/// For a package whose code reaches a player after its objects did. Spawned from a spawn menu for the first time while
/// a player was already in the game, the board arrived there seconds before the package's code had loaded (it compiles
/// there): its components were held as missing, and stayed so, the board neither shown nor ridable there and its rider
/// unable to find it. As the code loads (the engine hotloads it in), every component of this package held as missing is
/// made from what was held, under its own id, so the network and its calls find it; and the scene's systems are made.
/// </summary>
internal static class LateLoad
{
	[Event( "hotloaded" )]
	private static void Restore()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || scene.IsEditor )
			return;
		const string ours = nameof( SkateMotion ) + ".";
		foreach ( var missing in scene.Components.GetAll<MissingComponent>( FindMode.EverythingInSelfAndDescendants ).ToArray() )
		{
			var json = missing.GetJson();
			var name = json?[ "__type" ]?.ToString();
			if ( name is null || !name.StartsWith( ours ) || TypeLibrary.GetType<Component>( name, false, true ) is not { } type )
				continue;
			var holder = missing.GameObject;
			missing.Destroy();
			holder.Components.Create( type, false ).DeserializeImmediately( json );
		}
		SceneSystems.Ensure( scene );
	}
}
