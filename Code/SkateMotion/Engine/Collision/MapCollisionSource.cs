namespace SkateMotion.Engine.Collision;

using Sandbox;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Grinding;
using NumericsVector = System.Numerics.Vector3;

/// <summary>Owns an explicit map-geometry snapshot and updates body transforms before queries.</summary>
internal sealed partial class MapCollisionSource
{
	private readonly MeshWorldQuery _query;
	private readonly Dictionary<PhysicsShape, MapCollisionShape> _shapes = new();
	private readonly List<MapCollisionShape> _ordered = new();
	private readonly List<PhysicsShape> _removed = new();
	private MeshQueryInstance[] _instances = [];
	private readonly GrindEdgeRecord[] _candidateEdges = new GrindEdgeRecord[ GrindEdgeQuery.Capacity ];
	private readonly GrindScoringCandidate[] _scoringCandidates = new GrindScoringCandidate[ 7 ];
	private uint _nextIdentity;
	internal int ShapeCount => _ordered.Count;
	internal int TriangleCount { get; private set; }
	internal int UnsupportedShapeCount { get; private set; }
	internal int GeometryBuilds { get; private set; }

	internal MapCollisionSource( MeshWorldQuery query ) => _query = query;

	/// <summary>Runs candidate selection in query-space metres using the current map snapshot.</summary>
	internal bool SelectCandidate( GrindCandidateSelection selection, GrindCandidateSelectionInput input,
		GrindCandidateSelectionState state, GrindEdgeChunk[] chunks, TrajectoryQueryService service )
	{
		ArgumentNullException.ThrowIfNull( selection );
		UpdateTransforms();
		return selection.Evaluate( input, state, chunks, _query, _instances, service );
	}

	/// <summary>Processes queued requests in query-space metres through the current map snapshot.</summary>
	internal void ProcessTrajectories( TrajectoryQueryService service, TrajectoryWorldQuery trajectory )
	{
		ArgumentNullException.ThrowIfNull( service );
		ArgumentNullException.ThrowIfNull( trajectory );
		UpdateTransforms();
		service.Process( trajectory, _instances );
	}

	/// <summary>Evaluates quadratic motion in world-space metres through the current map snapshot.</summary>
	internal TrajectoryContact EvaluateTrajectory( TrajectoryWorldQuery trajectory, TrajectoryQueryRequest request )
	{
		ArgumentNullException.ThrowIfNull( trajectory );
		UpdateTransforms();
		request = request with
		{
			Position = RailEdgeSpace.ToLookup( request.Position ),
			Velocity = RailEdgeSpace.ToLookup( request.Velocity ),
			Acceleration = RailEdgeSpace.ToLookup( request.Acceleration )
		};
		var result = trajectory.Evaluate( request, _instances );
		return result with
		{
			Position = RailEdgeSpace.ToWorld( result.Position ),
			Normal = RailEdgeSpace.ToWorld( result.Normal ),
			SurfaceNormal = RailEdgeSpace.ToWorld( result.SurfaceNormal ),
			Transform = MapCollisionCoordinates.MatrixToWorld( result.Transform )
		};
	}

	internal void Refresh( PhysicsWorld world, Func<PhysicsShape, int, ushort> material )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( material );
		foreach ( var entry in _shapes.Values ) entry.Seen = false;
		_ordered.Clear();
		TriangleCount = 0;
		UnsupportedShapeCount = 0;
		foreach ( var body in world.Bodies )
		{
			if ( !body.IsValid() ) continue;
			foreach ( var shape in body.Shapes )
			{
				if ( !shape.IsValid() || shape.IsTrigger || !shape.EnableSolidCollisions ) continue;
				if ( !shape.IsMeshShape && !shape.IsHullShape && !shape.IsHeightfieldShape )
				{
					UnsupportedShapeCount++;
					continue;
				}
				if ( !_shapes.TryGetValue( shape, out var entry ) )
				{
					entry = new( shape, checked(++_nextIdentity) );
					_shapes.Add( shape, entry );
				}
				// Triangulate allocates in the engine. Refresh is an explicit geometry-change
				// operation; ordinary queries update only body transforms and enabled state.
				if ( entry.Refresh( material ) ) GeometryBuilds++;
				entry.Seen = true;
				_ordered.Add( entry );
				TriangleCount += entry.Geometry.Triangles.Length;
				if ( entry.Geometry.Triangles.Length == 0 ) UnsupportedShapeCount++;
			}
		}
		_removed.Clear();
		foreach ( var pair in _shapes )
		{
			if ( !pair.Value.Seen ) _removed.Add( pair.Key );
		}
		foreach ( var shape in _removed ) _shapes.Remove( shape );
		if ( _instances.Length != _ordered.Count ) _instances = new MeshQueryInstance[ _ordered.Count ];
		UpdateTransforms();
		BuildEdgeCodes();
	}

	internal void UpdateTransforms()
	{
		for ( var i = 0; i < _ordered.Count; i++ ) _instances[ i ] = _ordered[ i ].Instance();
	}

	internal bool SetQueryFilter( PhysicsShape shape, int filter, uint exclusion )
	{
		if ( !shape.IsValid() || !_shapes.TryGetValue( shape, out var entry ) ) return false;
		entry.Filter = filter;
		entry.Exclusion = exclusion;
		return true;
	}

	internal bool TryInvestigate( GrindSurfaceInvestigation investigation, RailSegment edge,
		NumericsVector reference, float reach, bool includeDownward, NumericsVector downwardOrigin,
		int filter, uint previousFlags, out GrindSurfaceClassification classification )
	{
		ArgumentNullException.ThrowIfNull( investigation );
		UpdateTransforms();
		var active = investigation.TryEvaluate( _query, _instances,
			new( RailEdgeSpace.ToLookup( edge.Start ), RailEdgeSpace.ToLookup( edge.End ) ),
			RailEdgeSpace.ToLookup( reference ), reach, includeDownward, RailEdgeSpace.ToLookup( downwardOrigin ),
			filter, previousFlags, out classification );
		classification = classification with
		{
			Center = RailEdgeSpace.ToWorld( classification.Center ),
			FirstFarPoint = RailEdgeSpace.ToWorld( classification.FirstFarPoint ),
			SecondFarPoint = RailEdgeSpace.ToWorld( classification.SecondFarPoint ),
			Up = RailEdgeSpace.ToWorld( classification.Up ),
			Direction = RailEdgeSpace.ToWorld( classification.Direction ),
			HighSide = RailEdgeSpace.ToWorld( classification.HighSide )
		};
		return active;
	}

	internal GrindSurfaceContact EvaluateContact( GrindSurfaceContactQuery contactQuery, RailSegment edge,
		NumericsVector approachReference, NumericsVector point, float reach, int filter, GrindSurfaceContact previous )
	{
		ArgumentNullException.ThrowIfNull( contactQuery );
		UpdateTransforms();
		var result = contactQuery.Evaluate( _query, _instances,
			new( RailEdgeSpace.ToLookup( edge.Start ), RailEdgeSpace.ToLookup( edge.End ) ),
			RailEdgeSpace.ToLookup( approachReference ), RailEdgeSpace.ToLookup( point ), reach, filter,
			MapCollisionCoordinates.ContactToQuery( previous ) );
		return MapCollisionCoordinates.ContactToWorld( result );
	}

	/// <summary>Evaluates supplied world-space edges against cached map geometry, preserving partial candidate state.</summary>
	internal bool TryAccept( GrindCandidateAcceptance acceptance, GrindEdgeRecord[] edges, int count,
		GrindCandidateInput input, ref GrindCandidateState state )
	{
		ArgumentNullException.ThrowIfNull( acceptance );
		ArgumentNullException.ThrowIfNull( edges );
		if ( count < 0 || count > edges.Length || count > _candidateEdges.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		UpdateTransforms();
		for ( var i = 0; i < count; i++ )
		{
			var edge = edges[ i ];
			_candidateEdges[ i ] = edge with
			{
				Segment = new( RailEdgeSpace.ToLookup( edge.Segment.Start ), RailEdgeSpace.ToLookup( edge.Segment.End ) )
			};
		}
		input = input with
		{
			Approach = RailEdgeSpace.ToLookup( input.Approach ),
			Reference = RailEdgeSpace.ToLookup( input.Reference ),
			Support = RailEdgeSpace.ToLookup( input.Support )
		};
		var queryState = MapCollisionCoordinates.CandidateToQuery( state );
		var accepted = acceptance.TryAccept( _candidateEdges, count, input, _query, _instances, ref queryState );
		state = MapCollisionCoordinates.CandidateToWorld( queryState );
		return accepted;
	}

	internal MapCollisionHit Trace( NumericsVector start, NumericsVector end, float radius, MeshQueryFilter surfaces )
		=> TraceFiltered( start, end, radius, surfaces, -1, 0 );

	/// <summary>Scores contacts from query-frame edge chunks using world-space input and retained state.</summary>
	internal float ProduceCandidate( GrindCandidateProducer producer, GrindEdgeChunk[] queryChunks,
		GrindCandidateInput input, ref GrindCandidateProductionState state )
	{
		ArgumentNullException.ThrowIfNull( producer );
		UpdateTransforms();
		input = input with
		{
			Approach = RailEdgeSpace.ToLookup( input.Approach ),
			Reference = RailEdgeSpace.ToLookup( input.Reference ),
			Support = RailEdgeSpace.ToLookup( input.Support )
		};
		var queryState = state with { Candidate = MapCollisionCoordinates.CandidateToQuery( state.Candidate ) };
		var score = producer.Evaluate( queryChunks, input, _query, _instances, ref queryState );
		state = queryState with { Candidate = MapCollisionCoordinates.CandidateToWorld( queryState.Candidate ) };
		return score;
	}

	/// <summary>Evaluates fallback rejection using world-space metres and retained vertical height.</summary>
	internal float ScoreRejection( GrindCandidateRejection rejection, GrindCandidateRejectionInput input,
		ref GrindTrajectoryState trajectory, ref float highestHeight )
	{
		ArgumentNullException.ThrowIfNull( rejection );
		UpdateTransforms();
		input = input with
		{
			Point = MapCollisionCoordinates.SpatialToQuery( input.Point ),
			Direction = MapCollisionCoordinates.SpatialToQuery( input.Direction )
		};
		var queryTrajectory = MapCollisionCoordinates.TrajectoryToQuery( trajectory );
		var score = rejection.Evaluate( input, _query, _instances, ref queryTrajectory, ref highestHeight );
		trajectory = MapCollisionCoordinates.TrajectoryToWorld( queryTrajectory );
		return score;
	}

	/// <summary>Scores world-space candidates against cached meshes and query-frame edge chunks; retained scorer frames stay in the query frame.</summary>
	internal void ScoreCandidates( GrindCandidateScoring scoring, GrindScoringCandidate[] candidates,
		short count, int minimumSample, GrindScoringContext context, GrindEdgeChunk[] queryChunks,
		GrindCandidateInput input, ref GrindCandidateProductionState state, ref float highestHeight )
	{
		ArgumentNullException.ThrowIfNull( scoring );
		ArgumentNullException.ThrowIfNull( candidates );
		if ( count > candidates.Length || count > _scoringCandidates.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		UpdateTransforms();
		for ( var i = 0; i < count; i++ )
			_scoringCandidates[ i ] = MapCollisionCoordinates.ScoringCandidateToQuery( candidates[ i ] );
		input = input with
		{
			Approach = RailEdgeSpace.ToLookup( input.Approach ),
			Reference = RailEdgeSpace.ToLookup( input.Reference ),
			Support = RailEdgeSpace.ToLookup( input.Support )
		};
		var queryState = state with { Candidate = MapCollisionCoordinates.CandidateToQuery( state.Candidate ) };
		scoring.Evaluate( _scoringCandidates, count, minimumSample, MapCollisionCoordinates.ScoringContextToQuery( context ),
			queryChunks, input, _query, _instances, ref queryState, ref highestHeight );
		for ( var i = 0; i < count; i++ )
			candidates[ i ] = MapCollisionCoordinates.ScoringCandidateToWorld( _scoringCandidates[ i ] );
		state = queryState with { Candidate = MapCollisionCoordinates.CandidateToWorld( queryState.Candidate ) };
	}

	internal MapCollisionHit TraceFiltered( NumericsVector start, NumericsVector end, float radius,
		MeshQueryFilter surfaces, int filter, uint exclusion )
	{
		UpdateTransforms();
		var result = _query.Evaluate( new( RailEdgeSpace.ToLookup( start ), RailEdgeSpace.ToLookup( end ),
			radius, filter, exclusion, surfaces ), _instances );
		PhysicsShape selected = null;
		foreach ( var entry in _ordered )
		{
			if ( entry.Identity == result.Identity && entry.Shape.IsValid() )
			{
				selected = entry.Shape;
				break;
			}
		}
		return new( result.HitCount != 0, RailEdgeSpace.ToWorld( result.Position ), RailEdgeSpace.ToWorld( result.Normal ),
			result.Fraction, result.Material, selected, result.HitCount );
	}
}
