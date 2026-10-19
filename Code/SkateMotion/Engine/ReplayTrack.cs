using System.Collections.Generic;
using Sandbox;

namespace SkateMotion.Engine;

/// <summary>Recorded fixed-step inputs with the resulting deck positions, stored as JSON (version 1).</summary>
internal sealed class ReplayTrack
{
	public int Version { get; set; } = 1;
	public float FixedDelta { get; set; }
	public Vector3 StartPosition { get; set; }
	public Rotation StartRotation { get; set; }
	public Vector3 StartVelocity { get; set; }
	public Vector3 StartAngularVelocity { get; set; }
	public List<SkateInput> Inputs { get; set; } = new();
	public List<Vector3> Positions { get; set; } = new();

	internal void Begin( Rigidbody body, float fixedDelta )
	{
		FixedDelta = fixedDelta;
		StartPosition = body.WorldPosition;
		StartRotation = body.WorldRotation;
		StartVelocity = body.Velocity;
		StartAngularVelocity = body.AngularVelocity;
		Inputs.Clear();
		Positions.Clear();
	}

	internal void Add( SkateInput input, Vector3 position )
	{
		Inputs.Add( input );
		Positions.Add( position );
	}

	internal void Save( string path ) => FileSystem.Data.WriteAllText( path, Json.Serialize( this ) );

	internal static ReplayTrack Load( string path )
	{
		if ( !FileSystem.Data.FileExists( path ) )
			throw new ArgumentException( $"No replay saved at '{path}'.", nameof( path ) );
		var track = Json.Deserialize<ReplayTrack>( FileSystem.Data.ReadAllText( path ) );
		if ( track is null || track.Version != 1 || track.Inputs.Count != track.Positions.Count )
			throw new ArgumentException( $"'{path}' is not a version 1 skate replay.", nameof( path ) );
		return track;
	}
}
