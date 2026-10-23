using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace SkateMotion.Systems;

/// <summary>
/// For a package whose code reaches a player after its objects did. Spawned from a spawn menu for the first time while a
/// player was already in the game, the board was made there a moment before the package's code was (the engine holds the
/// objects until the package is installed, then makes them before it registers the code): its components were held as
/// missing, and stay so (the engine does not make them once their code is there), the board neither shown nor ridable
/// there and its rider unable to find it. Sandbox spawns every entity so (EntitySpawner: Cloud.Load, Clone, NetworkSpawn on
/// the host), and the hotloaded event does not come for code added rather than replaced, so nothing tells the code it has
/// just loaded but this: the engine reads each console variable of a package as it registers the package's code
/// (ConVarSystem.AddAssembly), which runs this class's static constructor. A moment later, once, a player holding
/// components of this package as missing asks the host to send each again (Network.Refresh, the documented way to bring a
/// networked object's components to the other players): they are made from it there, their synced values and calls wired
/// in as any other's.
/// </summary>
internal static class LateLoad
{
	/// <summary>Whether a player asks the host for this package's components that reached them before its code.</summary>
	[ConVar( "skateboard_fetch_missing", Help = "Ask the host for skateboard parts that arrived before the skateboard's code" )]
	public static bool FetchMissing { get; set; } = true;

	// Registering FetchMissing reads it, which runs this as the code loads; the objects that came first are in the scene a
	// moment later.
	static LateLoad() => _ = CheckSoon();

	private static async Task CheckSoon()
	{
		await GameTask.DelaySeconds( 0.5f );
		var scene = Game.ActiveScene;
		if ( !FetchMissing || !Networking.IsActive || Networking.IsHost || !scene.IsValid() || scene.IsEditor )
			return;
		SceneSystems.Ensure( scene );
		Ask( scene );
	}

	// Asks the host for each component of this package held as missing here.
	private static void Ask( Scene scene )
	{
		const string ours = nameof( SkateMotion ) + ".";
		foreach ( var missing in scene.Components.GetAll<MissingComponent>( FindMode.EverythingInSelfAndDescendants ).ToArray() )
		{
			var json = missing.GetJson();
			if ( json?[ "__type" ]?.ToString() is { } type && type.StartsWith( ours )
				&& Guid.TryParse( json[ "__guid" ]?.ToString(), out var id ) )
				SendAgain( id );
		}
	}

	/// <summary>Host: sends one component again to every player, asked by one whose copy of it is missing.</summary>
	[Rpc.Host]
	public static void SendAgain( Guid component )
	{
		if ( Game.ActiveScene?.Directory.FindComponentByGuid( component ) is { } found && found.GameObject.IsValid() )
			found.GameObject.Network.Refresh( found );
	}
}
