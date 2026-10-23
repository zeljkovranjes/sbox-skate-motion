using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace SkateMotion.Systems;

/// <summary>
/// For a package whose code reaches a player after its objects did. Spawned from a spawn menu for the first time while a
/// player was already in the game, the board arrived there before the package's code had loaded (it takes seconds): its
/// components were held as missing, and stay so (the engine does not make them once their code is there), the board
/// neither shown nor ridable there and its rider unable to find it. Nothing tells a player's code that it has just loaded
/// (the hotloaded event does not reach code a package brings mid-game), so the host, which makes every such object with
/// its code there, tells every other player to look a few times as one is made (<see cref="Announce"/>); a player holding
/// components of this package as missing asks the host to send each again (Network.Refresh, the documented way to bring
/// a networked object's components to the other players), and they are made from it there, their synced values and calls
/// wired in as any other's.
/// </summary>
internal static class LateLoad
{
	// Seconds between the host's reminders after an object of this package is made: a player's code can take from a
	// moment to half a minute to load.
	private static readonly float[] Reminders = { 1f, 1f, 2f, 4f, 8f, 16f };

	/// <summary>Host: an object of this package was just made, which a player whose code is still loading got without it.</summary>
	internal static void Announce( Scene scene )
	{
		if ( Networking.IsActive && Networking.IsHost && scene.IsValid() && !scene.IsEditor )
			_ = Remind( scene );
	}

	private static async Task Remind( Scene scene )
	{
		foreach ( var wait in Reminders )
		{
			await GameTask.DelaySeconds( wait );
			if ( !scene.IsValid() )
				return;
			LookAgain();
		}
	}

	/// <summary>Every other player: asks the host for any component of this package held as missing here.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public static void LookAgain()
	{
		var scene = Game.ActiveScene;
		if ( Networking.IsHost || !scene.IsValid() || scene.IsEditor )
			return;
		SceneSystems.Ensure( scene );
		Ask( scene );
	}

	// Where the hotloaded event does come (code changed while a game runs), the same look.
	[Event( "hotloaded" )]
	private static void OnCodeLoaded()
	{
		if ( Networking.IsActive && !Networking.IsHost )
			LookAgain();
	}

	// Asks the host for each component of this package held as missing here, and lets each one's placeholder go once the
	// host's has come.
	private static void Ask( Scene scene )
	{
		const string ours = nameof( SkateMotion ) + ".";
		foreach ( var missing in scene.Components.GetAll<MissingComponent>( FindMode.EverythingInSelfAndDescendants ).ToArray() )
		{
			var json = missing.GetJson();
			if ( json?[ "__type" ]?.ToString() is not { } type || !type.StartsWith( ours )
				|| !Guid.TryParse( json[ "__guid" ]?.ToString(), out var id ) )
				continue;
			if ( scene.Directory.FindComponentByGuid( id ).IsValid() )
				missing.Destroy();
			else
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
