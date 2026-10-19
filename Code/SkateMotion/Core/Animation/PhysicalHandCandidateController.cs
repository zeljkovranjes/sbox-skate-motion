namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Refreshes candidate trajectories, frames and metric after surface classification.</summary>
internal sealed class PhysicalHandCandidateController
{
	private readonly LimbSolverMath _math;
	private readonly GrindEdgeSelectionCurve _response;
	private readonly float _distance;
	private readonly float _angle;
	private readonly float _eligibility;

	internal PhysicalHandCandidateController( LimbSolverMath math, GrindEdgeSelectionCurve response,
		float distance, float angle, float eligibility )
	{
		ArgumentNullException.ThrowIfNull( math );
		ArgumentNullException.ThrowIfNull( response );
		_math = math;
		_response = response;
		_distance = distance;
		_angle = angle;
		_eligibility = eligibility;
	}

	internal void Update( PhysicalHandCandidateState state, PhysicalHandCandidateInput input,
		GrindSurfaceQueryResult surface )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( surface );
		if ( state.Pending == 0 )
			return;
		state.Pending = 0;
		if ( !input.Enabled || !surface.TryConsume( out var classification )
			|| classification.Kind == GrindSurfaceKind.Blocked )
			return;

		var side = Cross( input.Edge.Segment.End - input.Edge.Segment.Start, Vector3.UnitY );
		var sign = Dot( side, input.Normal ) > 0f ? -1f : 1f;
		side = Scale( NormalizeOrZero( side ), sign );
		state.OppositeSide = -side;
		var (sine, cosine) = _math.SinCos( _angle );
		var offset = Scale( Vector3.UnitY, MultiplyAdd( _distance, cosine, 0f ) );
		offset = AddScaled( side, MultiplyAdd( _distance, sine, 0f ), offset );
		var destination = input.Point + offset;
		state.Trajectory = BuildTrajectory( input.Origin, destination );
		state.StartTime = 0f;
		var velocity = state.Trajectory.Velocity.Y;
		var upwardGravity = MultiplyAdd( -1f, state.Trajectory.Acceleration.Y, 0f );
		state.ApexTime = velocity < 0f || 0f >= upwardGravity ? 0f
			: MultiplyAdd( Reciprocal( upwardGravity ), velocity, 0f );
		state.FirstTrajectory = state.Trajectory;
		state.SecondTrajectory = state.Trajectory;
		state.Edge = input.Edge with { Flags = (state.Edge.Flags & 0x7fffffffu) | (input.Edge.Flags & 0x80000000u) };
		state.Normal = input.Normal;
		state.Down = new Vector3( 0f, -1f, 0f );
		state.ReferenceNormal = input.Normal;

		var apex = PositionAt( state.Trajectory, state.ApexTime );
		state.StartFrame = MakeFrame( input.Tangent, input.Normal ) with
		{
			Translation = PositionAt( state.Trajectory, 0f )
		};
		state.SideSign = Dot( input.Tangent, input.Approach ) > 0f ? 1f : -1f;
		var edgeDirection = NormalizeOrZero( input.Edge.Segment.Start - input.Edge.Segment.End );
		var horizontal = edgeDirection with { Y = 0f };
		var facing = Dot( horizontal, apex - input.Origin ) > 0f ? 1f : -1f;
		var direction = Scale( edgeDirection, MultiplyAdd( facing, state.SideSign, 0f ) );
		state.ApexFrame = MakeFrame( direction, NormalizeOrZero( input.Point - apex ) ) with { Translation = apex };
		state.ContactFrame = MakeFrame( Scale( state.Down, state.SideSign ), input.Normal );
		UpdateMetric( state );
		state.Point = input.Point;
		var flags = (uint)state.Flags | 0x80000000u;
		flags = SetFlag( flags, 30, Length( input.PreviousPoint - input.Point ) > .15000000596046448f );
		flags = SetFlag( flags, 29, 0f > Dot( input.Point - input.Origin, input.Side ) );
		flags = SetFlag( flags, 28, state.Metric.X < _eligibility );
		state.Flags = ((ulong)BitConverter.SingleToUInt32Bits( state.Metric.X ) << 32) | flags;
	}

	private void UpdateMetric( PhysicalHandCandidateState state )
	{
		var delta = state.StartTime - state.ApexTime;
		var step = MathF.Abs( delta * .3333333432674408f );
		var sample = delta;
		var sum = 0f;
		for ( var i = 0; i < 4; i++ )
		{
			sum += _response.Evaluate( sample ) + 1f;
			sample += step;
		}
		var average = sum * .25f;
		if ( average != 0f )
			state.Metric = new Vector4( -delta / average );
	}

	private static GrindTrajectoryState BuildTrajectory( Vector3 origin, Vector3 destination )
	{
		var gravity = new Vector4( 0f, -9.800000190734863f, 0f, 1f );
		var position = new Vector4( origin, 0f );
		var delta = new Vector4( destination, 0f ) - position;
		var squared = MultiplyAdd( MultiplyAdd( -2f, gravity.Y, 0f ), delta.Y, 0f );
		var duration = MultiplyAdd( Reciprocal( gravity.Y ), -SquareRoot( squared ), 0f );
		var acceleration = Scale4( gravity, Reciprocal( 2f ) );
		acceleration = Scale4( Scale4( acceleration, duration ), duration );
		return new GrindTrajectoryState( position, Scale4( delta - acceleration, Reciprocal( duration ) ), gravity, new Vector4( -1f ) );
	}

	private static Vector3 PositionAt( GrindTrajectoryState trajectory, float time )
	{
		var value = trajectory.Position + Scale4( trajectory.Velocity, time );
		value += Scale4( Scale4( trajectory.Acceleration, MultiplyAdd( 1f, .5f, 0f ) ), MultiplyAdd( time, time, 0f ) );
		return new Vector3( value.X, value.Y, value.Z );
	}

	private static LimbRigidTransform MakeFrame( Vector3 first, Vector3 second )
	{
		if ( MathF.Abs( Dot( first, second ) ) > .9999899864196777f )
		{
			first = Vector3.UnitX;
			if ( MathF.Abs( Dot( first, second ) ) > .9999899864196777f )
				first = Vector3.UnitZ;
		}
		var x = NormalizeOrZero( Cross( second, first ) );
		var z = NormalizeOrZero( Cross( x, second ) );
		var y = Normalize( Cross( z, x ) );
		return new LimbRigidTransform( new LimbFrame( x, y, z ), Vector3.Zero );
	}

	private static Vector3 NormalizeOrZero( Vector3 value ) =>
		Length( value ) > .000001f ? Normalize( value ) : Vector3.Zero;

	private static float Length( Vector3 value ) => SquareRoot( Dot( value, value ) );

	private static Vector4 Scale4( Vector4 value, float scale ) => new(
		MultiplyAdd( value.X, scale, 0f ), MultiplyAdd( value.Y, scale, 0f ),
		MultiplyAdd( value.Z, scale, 0f ), MultiplyAdd( value.W, scale, 0f ) );

	private static uint SetFlag( uint flags, int bit, bool enabled ) =>
		(flags & ~(1u << bit)) | (enabled ? 1u << bit : 0u);
}
