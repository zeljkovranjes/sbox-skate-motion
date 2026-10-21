using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

/// <summary>A clip set and the graph that plays it, read from the game's data folder.</summary>
internal sealed class RiderAnimationAssets
{
	private RiderAnimationAssets( ClipSet clips, AnimationGraphDefinition graph )
	{
		Clips = clips;
		Graph = graph;
		if ( graph?.ClipBoneWeights is not null )
		{
			foreach ( var (name, weights) in graph.ClipBoneWeights )
			{
				var clip = clips.Find( name ) ?? throw new FormatException( $"Bone weights name missing clip {name}." );
				clip.SetBoneWeights( weights );
			}
		}
		if ( graph?.ClipLooping is not null )
		{
			foreach ( var (name, looping) in graph.ClipLooping )
			{
				var clip = clips.Find( name ) ?? throw new FormatException( $"Looping metadata names missing clip {name}." );
				clip.Looping = looping;
			}
		}
		Rig = graph?.Rig is null ? null : new EffectorRigAsset( graph.Rig, clips.Skeleton );
		Hands = LoadHands( graph?.HandPoses, clips.Skeleton );
		RetrievalProfile = BoardRetrievalProfile.Load( graph?.BoardRetrievalProfilePath );
		if ( !string.IsNullOrWhiteSpace( graph?.PostureProfilePath ) )
		{
			if ( !FileSystem.Data.FileExists( graph.PostureProfilePath ) )
				throw new FormatException( $"Posture profile is missing: {graph.PostureProfilePath}" );
			var profile = Json.Deserialize<AnimationPostureProfile>( FileSystem.Data.ReadAllText( graph.PostureProfilePath ) )
				?? throw new FormatException( "Posture profile is empty." );
			Posture = profile.CreateProvider( clips.Skeleton );
		}
	}

	internal ClipSet Clips { get; }
	internal AnimationGraphDefinition Graph { get; }
	internal EffectorRigAsset Rig { get; }
	internal BoardRetrievalProfile RetrievalProfile { get; }
	internal AnimationPostureTreeProvider Posture { get; }

	/// <summary>Finger pose banks, one per hand; empty when the graph names none or one cannot be read.</summary>
	internal IReadOnlyList<HandPoseBank> Hands { get; }

	// A missing or unreadable bank leaves the fingers to follow their hands rather than losing the clips.
	private static IReadOnlyList<HandPoseBank> LoadHands( List<HandPoseDefinition> hands, ClipSkeleton skeleton )
	{
		if ( hands is null || hands.Count == 0 )
			return System.Array.Empty<HandPoseBank>();
		var banks = new List<HandPoseBank>();
		foreach ( var hand in hands )
		{
			if ( !FileSystem.Data.FileExists( hand.Bank ) )
			{
				Log.Warning( $"Hand pose bank not found in the data folder: {hand.Bank}" );
				return System.Array.Empty<HandPoseBank>();
			}
			try
			{
				var bones = RawAnimationClipSet.Read( FileSystem.Data.ReadAllBytes( hand.Bank ) );
				if ( skeleton.IndexOf( hand.Hand ) < 0 || bones.Find( hand.Clip ) is null
					|| bones.Names.Any( name => skeleton.IndexOf( name ) >= 0 || banks.Any( bank => bank.Bones.Names.Contains( name ) ) ) )
					throw new FormatException( $"it needs a clip {hand.Clip}, a hand bone {hand.Hand} in the clips and bone names of its own" );
				banks.Add( new HandPoseBank( hand.Hand, AnimationPoseStreamKey.Encode( hand.Stream ), bones, hand.Clip ) );
			}
			catch ( Exception error ) when ( error is FormatException or ArgumentException )
			{
				Log.Warning( $"Hand pose bank {hand.Bank} cannot be used: {error.Message}" );
				return System.Array.Empty<HandPoseBank>();
			}
		}
		return banks;
	}

	/// <summary>Loads both files from <see cref="FileSystem.Data"/>; returns null and warns when either is missing.</summary>
	internal static RiderAnimationAssets Load( string clipsPath, string graphPath )
	{
		if ( string.IsNullOrWhiteSpace( clipsPath ) || string.IsNullOrWhiteSpace( graphPath ) )
			return null;
		if ( !FileSystem.Data.FileExists( clipsPath ) || !FileSystem.Data.FileExists( graphPath ) )
		{
			Log.Warning( $"Rider animation files not found in the data folder ({FileSystem.Data.GetFullPath( "" )}): {clipsPath}, {graphPath}" );
			return null;
		}
		var clips = ClipSet.Read( FileSystem.Data.ReadAllBytes( clipsPath ) );
		var graph = Json.Deserialize<AnimationGraphDefinition>( FileSystem.Data.ReadAllText( graphPath ) );
		if ( !string.IsNullOrWhiteSpace( graph.RawPosePath ) )
		{
			if ( string.IsNullOrWhiteSpace( graph.AttributeBankPath ) )
				throw new FormatException( "Raw rider poses require an accompanying attribute bank." );
			var attributes = AnimationClipAttributeBank.Read( FileSystem.Data.ReadAllBytes( graph.AttributeBankPath ) );
			var poses = RawAnimationClipSet.ReadWithAttributes( FileSystem.Data.ReadAllBytes( graph.RawPosePath ), attributes );
			clips.BindRawPoses( poses );
		}
		return new RiderAnimationAssets( clips, graph );
	}
}
