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
