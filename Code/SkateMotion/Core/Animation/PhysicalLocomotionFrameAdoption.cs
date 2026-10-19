namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Adopts the solved offboard frame after its retained turn correction and stance adjustment.</summary>
internal sealed class PhysicalLocomotionFrameAdoption
{
	private readonly LimbSolverMath _math;

	internal PhysicalLocomotionFrameAdoption( LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
	}

	internal void Update( PhysicalLocomotionFrameState state, PhysicalRigState rig,
		in PhysicalLocomotionMotionState solved, ulong actionFlags )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( rig );
		var axes = new LimbFrame( solved.FrameRow0, solved.FrameRow1, solved.FrameRow2 );
		if ( state.TurnEnabled )
		{
			state.TurnRemaining -= 1f / 60f;
			if ( state.TurnRemaining <= 0f )
			{
				state.TurnRemaining = 0f;
				state.TurnEnabled = false;
			}
			state.TurnAngle *= .9f;
			var angle = SubtractProduct( state.TurnRate, state.TurnRemaining, state.TurnAngle );
			var (sine, cosine) = _math.SinCos( angle );
			var rotation = new LimbFrame( new( cosine, 0f, -sine ), Vector3.UnitY, new( sine, 0f, cosine ) );
			var z = rotation.Transform( axes.Z, Vector3.Zero );
			var x = Normalize( Cross( axes.Y, z ) );
			axes = new( x, Normalize( Cross( z, x ) ), z );
		}

		state.QueryAxes = axes;
		if ( (actionFlags & 4UL) != 0 )
			axes = new( Scale( axes.X, -1f ), axes.Y, Scale( axes.Z, -1f ) );
		var frame = new LimbRigidTransform( axes, solved.Position );
		rig.Preparation.ModelToWorld = frame;
		state.WorldToModel = frame.RigidInverse();
		state.FrameDirty = true;
	}
}
