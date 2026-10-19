using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>An independently matched collection assigned to one controller stick.</summary>
internal sealed class ControllerGestureSetProfile
{
	public int Lane { get; set; }
	public byte MaximumGap { get; set; }
	public ControllerGesturePatternProfile[] Patterns { get; set; }

	internal GestureRecognitionSet CreateSet()
	{
		if ( Patterns is null )
			throw new ArgumentException( "Gesture collection paths are missing." );
		var paths = new GesturePattern[ Patterns.Length ];
		var identities = new GestureCommandIdentity[ Patterns.Length ];
		for ( var index = 0; index < Patterns.Length; index++ )
		{
			var pattern = Patterns[ index ] ?? throw new ArgumentException( "Gesture path is empty." );
			paths[ index ] = pattern.CreatePattern();
			identities[ index ] = new( pattern.Name );
		}
		return new( Lane, paths, identities, MaximumGap );
	}
}
