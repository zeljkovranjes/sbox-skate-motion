using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Retains the board's initial orientation while placing it at a supplied directional offset.</summary>
internal sealed class BoardPlacementMotion
{
	private readonly float _offset;
	private readonly float _step;

	internal BoardPlacementMotion( float offset, float step )
	{
		if ( !float.IsFinite( offset ) || !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentException( "Placement requires a finite offset and positive finite step." );
		_offset = offset;
		_step = step;
	}

	internal static bool IsBeyondRange( Vector3 boardPosition, Vector3 referencePosition, float maximumDistance )
	{
		var difference = boardPosition - referencePosition;
		return maximumDistance * maximumDistance < Dot( difference, difference );
	}

	internal static void Begin( PhysicalBoardMotionState state, LimbRigidTransform board )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Source = board;
		state.Target = LimbRigidTransform.Identity;
		state.Output = LimbRigidTransform.Identity;
		state.Elapsed = 0f;
		state.Duration = 0f;
		state.Progress = 0f;
		state.Weight = 0f;
	}

	internal void Update( PhysicalBoardMotionState state, Vector3 referencePosition, Vector3 direction )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Elapsed += _step;
		state.Progress = .5f;
		state.Weight = .5f;
		state.Target = new LimbRigidTransform( state.Source.Rotation, AddScaled( direction, _offset, referencePosition ) );
		state.Output = state.Target;
	}
}
