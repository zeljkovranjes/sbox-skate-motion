using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

/// <summary>Retains the input physical frame while observing current canonical bones during output.</summary>
internal sealed class BoardHandlingPhysicalBinding
{
	private const int SpineIndex = 11;
	private readonly PhysicalRigState _rig;
	private readonly PhysicalBoardInputState _boardInput;
	private readonly LimbArcTangent _arcTangent;
	private LimbRigidTransform _inputFrame;
	private bool _hasInput;

	internal BoardHandlingPhysicalBinding( PhysicalRigState rig, PhysicalBoardInputState boardInput,
		LimbArcTangent arcTangent )
	{
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( boardInput );
		ArgumentNullException.ThrowIfNull( arcTangent );
		if ( rig.Rig.Skeleton.MutableBones.Length <= SpineIndex )
			throw new ArgumentException( "The physical rig must contain the canonical spine.", nameof( rig ) );
		_rig = rig;
		_boardInput = boardInput;
		_arcTangent = arcTangent;
	}

	internal void CaptureInput( ulong actionFlags )
	{
		_inputFrame = _rig.Preparation.ModelToWorld;
		if ( (actionFlags & 4UL) != 0 )
		{
			var axes = _inputFrame.Rotation;
			_inputFrame = _inputFrame with
			{
				Rotation = new( Scale( axes.X, -1f ), axes.Y, Scale( axes.Z, -1f ) )
			};
		}
		_hasInput = true;
	}

	internal BoardHandlingInput Publish( in BoardHandlingInput input, int boardState,
		PhysicalBoardMotionState boardMotion )
	{
		ArgumentNullException.ThrowIfNull( boardMotion );
		if ( !_hasInput )
			throw new InvalidOperationException( "Capture the physical input frame before publishing handling angles." );

		// The spine is current output state; the orientation frame remains the input snapshot.
		var spine = _rig.Preparation.ModelToWorld.Transform( _rig.Rig.Skeleton.MutableBones[ SpineIndex ].Translation );
		return BoardHandlingOrientation.Publish( input, _inputFrame, spine,
			_boardInput.PhysicalFrame.Translation, boardState, boardMotion.Source.Translation, _arcTangent );
	}
}
