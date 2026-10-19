using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>Records a board's fixed-step inputs and replays them from the same start to validate repeatability.</summary>
[Title( "Skate Replay" ), Category( "Skate Motion" ), Icon( "replay" )]
public sealed class SkateReplay : Component
{
	/// <summary>Board to record and replay; empty uses a Skateboard on this object.</summary>
	[Property] public Skateboard Board { get; set; }

	/// <summary>True while inputs are being captured.</summary>
	public bool IsRecording { get; private set; }

	/// <summary>True while a recording drives the board.</summary>
	public bool IsPlaying { get; private set; }

	/// <summary>Recorded fixed steps.</summary>
	public int Frames => _track.Inputs.Count;

	/// <summary>Largest deck distance from the recorded path during the last playback, in inches.</summary>
	public float MaximumDeviation { get; private set; }

	private ReplayTrack _track = new();
	private int _cursor;

	protected override void OnStart() => Board ??= Components.Get<Skateboard>();

	/// <summary>Starts a new recording from the board's current pose.</summary>
	public void StartRecording()
	{
		RequireBoard();
		StopPlayback();
		_track.Begin( Board.Body, Time.Delta );
		Board.Stepped += Record;
		IsRecording = true;
	}

	/// <summary>Stops recording; the track is kept for playback or saving.</summary>
	public void StopRecording()
	{
		if ( Board.IsValid() )
			Board.Stepped -= Record;
		IsRecording = false;
	}

	/// <summary>Resets the board to the recorded start and feeds the recorded inputs one per fixed step.</summary>
	public void Play()
	{
		RequireBoard();
		StopRecording();
		if ( Frames == 0 )
			throw new InvalidOperationException( "Nothing recorded to play." );
		if ( MathF.Abs( _track.FixedDelta - Time.Delta ) > 0.0001f )
			Log.Warning( $"Replay recorded at {_track.FixedDelta}s steps is playing at {Time.Delta}s; trajectories will differ." );
		Board.WorldPosition = _track.StartPosition;
		Board.WorldRotation = _track.StartRotation;
		Board.Body.Velocity = _track.StartVelocity;
		Board.Body.AngularVelocity = _track.StartAngularVelocity;
		Board.ResetSimulation();
		_cursor = 0;
		MaximumDeviation = 0f;
		Board.InputOverride = NextInput;
		Board.Stepped += Compare;
		IsPlaying = true;
	}

	/// <summary>Saves the current recording under the package's data folder.</summary>
	public void Save( string path ) => _track.Save( path );

	/// <summary>Loads a recording saved with Save.</summary>
	public void Load( string path ) => _track = ReplayTrack.Load( path );

	protected override void OnDisabled()
	{
		StopRecording();
		StopPlayback();
	}

	private void Record( SkateInput input ) => _track.Add( input, Board.WorldPosition );

	private SkateInput NextInput() => _cursor < Frames ? _track.Inputs[ _cursor ] : default;

	private void Compare( SkateInput input )
	{
		if ( _cursor < Frames )
			MaximumDeviation = MathF.Max( MaximumDeviation, Board.WorldPosition.Distance( _track.Positions[ _cursor ] ) );
		_cursor++;
		if ( _cursor >= Frames )
			StopPlayback();
	}

	private void StopPlayback()
	{
		if ( Board.IsValid() )
		{
			Board.InputOverride = null;
			Board.Stepped -= Compare;
		}
		IsPlaying = false;
	}

	private void RequireBoard()
	{
		if ( !Board.IsValid() )
			throw new InvalidOperationException( "Assign a Skateboard before recording or playing." );
	}
}
