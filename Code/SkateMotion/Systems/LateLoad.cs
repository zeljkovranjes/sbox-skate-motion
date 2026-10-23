using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace SkateMotion.Systems;

/// <summary>
/// For a package whose code reaches a player after its objects did. Spawned from a spawn menu for the first time while a
/// player was already in the game, the board arrived there before the package's code had loaded (it takes seconds): its
/// components were held as missing, and stay so (the engine does not make them once their code is there), the board
/// neither shown nor ridable there and its rider unable to find it. Once the code has loaded there, that player asks the
/// host to send each of them again (Network.Refresh, the documented way to bring a networked object's components to the
/// other players): they are made from it there, their synced values and calls wired in as any other's. The scene's
/// systems, made as a scene starts, are made too.
/// </summary>
internal static class LateLoad
{
	// How often (seconds) and how many times, once the code has loaded, a player looks for components held as missing: an
	// object on its way as the code loaded arrives just after it.
	private const float Every = 0.5f;
	private const int Looks = 20;

	[Event( "hotloaded" )]
	private static void OnCodeLoaded()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || scene.IsEditor )
			return;
		SceneSystems.Ensure( scene );
		// The host made every object of this package with its code there; only another player can have got one first.
		if ( Networking.IsActive && !Networking.IsHost )
			_ = Watch( scene );
	}

	private static async Task Watch( Scene scene )
	{
		var asked = new HashSet<Guid>();
		for ( var look = 0; look < Looks && scene.IsValid(); look++ )
		{
			Ask( scene, asked );
			await GameTask.DelaySeconds( Every );
		}
	}

	// Asks the host, once, for each component of this package held as missing here, and lets each one's placeholder go
	// once the host's has come.
	private static void Ask( Scene scene, HashSet<Guid> asked )
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
			else if ( asked.Add( id ) )
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
