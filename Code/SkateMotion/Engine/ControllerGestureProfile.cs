using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>Locally supplied recognizer collections, filtering and hold eligibility.</summary>
internal sealed class ControllerGestureProfile
{
	public float ComponentThreshold { get; set; }
	public int QualityMode { get; set; }
	public string[] HoldPatterns { get; set; }
	public ControllerGestureSetProfile[] Sets { get; set; }

	internal GestureInputPipeline CreatePipeline( int player )
	{
		if ( Sets is null || HoldPatterns is null )
			throw new ArgumentException( "Gesture profile requires collections and hold eligibility." );
		var sets = new GestureRecognitionSet[ Sets.Length ];
		for ( var index = 0; index < sets.Length; index++ )
			sets[ index ] = (Sets[ index ] ?? throw new ArgumentException( "Gesture collection is empty." )).CreateSet();
		var holds = new GestureCommandIdentity[ HoldPatterns.Length ];
		for ( var index = 0; index < holds.Length; index++ )
			holds[ index ] = new( HoldPatterns[ index ] );
		return new( sets, new( player, holds ), ComponentThreshold );
	}
}
