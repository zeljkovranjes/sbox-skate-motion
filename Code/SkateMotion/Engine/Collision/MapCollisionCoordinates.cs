namespace SkateMotion.Engine.Collision;

using SkateMotion.Core.Grinding;
using SkateMotion.Core.Animation;
using NumericsVector = System.Numerics.Vector3;
using NumericsVector4 = System.Numerics.Vector4;
using NumericsMatrix = System.Numerics.Matrix4x4;

/// <summary>Converts engine inches and Z-up axes to the collision query frame.</summary>
internal static class MapCollisionCoordinates
{
	internal static NumericsVector Point( Vector3 value ) => RailEdgeSpace.ToLookup( SkateUnits.Metres( value ) );

	internal static NumericsVector Direction( Vector3 value ) => RailEdgeSpace.ToLookup( SkateUnits.ToCore( value ) );

	internal static NumericsMatrix MatrixToWorld( NumericsMatrix value ) => new(
		value.M33, value.M31, value.M32, value.M34,
		value.M13, value.M11, value.M12, value.M14,
		value.M23, value.M21, value.M22, value.M24,
		value.M43, value.M41, value.M42, value.M44 );

	internal static NumericsMatrix MatrixToQuery( NumericsMatrix value ) => new(
		value.M22, value.M23, value.M21, value.M24,
		value.M32, value.M33, value.M31, value.M34,
		value.M12, value.M13, value.M11, value.M14,
		value.M42, value.M43, value.M41, value.M44 );

	internal static GrindSurfaceContact ContactToQuery( GrindSurfaceContact contact ) => contact with
	{
		Normal = RailEdgeSpace.ToLookup( contact.Normal ),
		Side = RailEdgeSpace.ToLookup( contact.Side ),
		FirstDirection = RailEdgeSpace.ToLookup( contact.FirstDirection ),
		SecondDirection = RailEdgeSpace.ToLookup( contact.SecondDirection ),
		HighSide = RailEdgeSpace.ToLookup( contact.HighSide )
	};

	internal static GrindSurfaceContact ContactToWorld( GrindSurfaceContact contact ) => contact with
	{
		Normal = RailEdgeSpace.ToWorld( contact.Normal ),
		Side = RailEdgeSpace.ToWorld( contact.Side ),
		FirstDirection = RailEdgeSpace.ToWorld( contact.FirstDirection ),
		SecondDirection = RailEdgeSpace.ToWorld( contact.SecondDirection ),
		HighSide = RailEdgeSpace.ToWorld( contact.HighSide )
	};

	internal static GrindCandidateState CandidateToQuery( GrindCandidateState state ) => Candidate( state, true );

	internal static GrindCandidateState CandidateToWorld( GrindCandidateState state ) => Candidate( state, false );

	internal static GrindScoringCandidate ScoringCandidateToQuery( GrindScoringCandidate candidate ) => candidate with
	{
		Point = SpatialToQuery( candidate.Point ),
		Direction = SpatialToQuery( candidate.Direction ),
		Axis = SpatialToQuery( candidate.Axis ),
		Trajectory = TrajectoryToQuery( candidate.Trajectory ),
		ContactFrame = MatrixToQuery( candidate.ContactFrame )
	};

	internal static GrindScoringCandidate ScoringCandidateToWorld( GrindScoringCandidate candidate ) => candidate with
	{
		Point = SpatialToWorld( candidate.Point ),
		Direction = SpatialToWorld( candidate.Direction ),
		Axis = SpatialToWorld( candidate.Axis ),
		Trajectory = TrajectoryToWorld( candidate.Trajectory ),
		ContactFrame = MatrixToWorld( candidate.ContactFrame )
	};

	internal static GrindScoringContext ScoringContextToQuery( GrindScoringContext context ) => context with
	{
		Up = SpatialToQuery( context.Up ),
		Velocity = SpatialToQuery( context.Velocity ),
		Projection = SpatialToQuery( context.Projection )
	};

	internal static GrindScoringContext ScoringContextToWorld( GrindScoringContext context ) => context with
	{
		Up = SpatialToWorld( context.Up ),
		Velocity = SpatialToWorld( context.Velocity ),
		Projection = SpatialToWorld( context.Projection )
	};

	internal static NumericsVector4 SpatialToQuery( NumericsVector4 value ) => new( value.Y, value.Z, value.X, value.W );

	internal static NumericsVector4 SpatialToWorld( NumericsVector4 value ) => new( value.Z, value.X, value.Y, value.W );

	internal static GrindTrajectoryState TrajectoryToQuery( GrindTrajectoryState state ) => state with
	{
		Position = SpatialToQuery( state.Position ),
		Velocity = SpatialToQuery( state.Velocity ),
		Acceleration = SpatialToQuery( state.Acceleration )
	};

	internal static GrindTrajectoryState TrajectoryToWorld( GrindTrajectoryState state ) => state with
	{
		Position = SpatialToWorld( state.Position ),
		Velocity = SpatialToWorld( state.Velocity ),
		Acceleration = SpatialToWorld( state.Acceleration )
	};

	private static GrindCandidateState Candidate( GrindCandidateState state, bool toQuery ) => state with
	{
		Trajectory = state.Trajectory with
		{
			Position = Convert( state.Trajectory.Position, toQuery ),
			Velocity = Convert( state.Trajectory.Velocity, toQuery ),
			Acceleration = Convert( state.Trajectory.Acceleration, toQuery )
		},
		Normal = Convert( state.Normal, toQuery ),
		EdgeUp = Convert( state.EdgeUp, toQuery ),
		Side = Convert( state.Side, toQuery ),
		HighSide = Convert( state.HighSide, toQuery ),
		Edge = new( Convert( state.Edge.Start, toQuery ), Convert( state.Edge.End, toQuery ) ),
		Contact = toQuery ? ContactToQuery( state.Contact ) : ContactToWorld( state.Contact )
	};

	private static NumericsVector Convert( NumericsVector value, bool toQuery )
		=> toQuery ? RailEdgeSpace.ToLookup( value ) : RailEdgeSpace.ToWorld( value );

	internal static NumericsMatrix Frame( Vector3 position, Rotation rotation )
	{
		var x = Direction( rotation * Vector3.Left );
		var y = Direction( rotation * Vector3.Up );
		var z = Direction( rotation * Vector3.Forward );
		var translation = Point( position );
		return new( x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f,
			translation.X, translation.Y, translation.Z, 0f );
	}
}
