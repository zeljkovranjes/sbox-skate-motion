using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateQueryTransferTests
{
	private static TrajectoryQueryRequest Request => new( new( 1, 2, 3 ), new( 4, 5, 6 ), new( 7, 8, 9 ),
		4, .2f, .25f, 1.2f, -1, 0, new( 0, 0, 0, 0, true, true ) )
	{
		PositionAuxiliary = 11, VelocityAuxiliary = 12, AccelerationAuxiliary = 13
	};
	private static TrajectoryContact Contact => new( new( 3, 4, 5 ), Vector3.UnitZ, Vector3.UnitY, .5f,
		Matrix4x4.CreateTranslation( 1, 2, 3 ), 30, 6 << 7, 123 );

	[Fact]
	public void SubmissionResetsContactAndCopiesCompleteRequestData()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		candidates[ 0 ].CorrectionAuxiliary = 9;
		queries.Targets[ 0 ].Reset( Request, 7 );
		new GrindCandidateQueryTransfer().TrackRequests( queries, candidates );
		var candidate = candidates[ 0 ];
		Assert.Equal( new Vector4( 1, 2, 3, 11 ), candidate.Trajectory.Position );
		Assert.Equal( new Vector4( 4, 5, 6, 12 ), candidate.Trajectory.Velocity );
		Assert.Equal( new Vector4( 7, 8, 9, 13 ), candidate.Trajectory.Acceleration );
		Assert.Equal( new Vector4( 4 ), candidate.Trajectory.Auxiliary );
		Assert.Equal( Vector4.Zero, candidate.Point );
		Assert.Equal( Vector4.UnitY, candidate.Direction );
		Assert.Equal( Vector4.UnitY, candidate.Axis );
		Assert.Equal( new Vector4( -1 ), candidate.Time );
		Assert.Equal( -1, candidate.SampleIndex );
		Assert.Equal( TrajectoryContact.Empty.Transform, candidate.ContactFrame );
		Assert.Equal( 0u, candidate.Material );
		Assert.Equal( 0u, candidate.Identity );
		Assert.Equal( Request.Radius, candidate.Radius );
		Assert.Equal( Request.FirstStep, candidate.FirstStep );
		Assert.Equal( Request.LaterStep, candidate.LaterStep );
		Assert.Equal( 7, candidate.RequestFlag );
		Assert.Equal( 9, candidate.CorrectionAuxiliary );
	}

	[Fact]
	public void DeliveredHitCopiesFrameAndMaterialKindWithoutReplacingTrajectory()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		var transfer = new GrindCandidateQueryTransfer();
		queries.Targets[ 0 ].Reset( Request, 5 );
		transfer.TrackRequests( queries, candidates );
		var corrected = candidates[ 0 ].Trajectory with { Velocity = new Vector4( 25 ) };
		candidates[ 0 ].Trajectory = corrected;
		queries.Targets[ 0 ].Deliver( Contact );
		transfer.Refresh( queries, candidates );
		Assert.Equal( corrected, candidates[ 0 ].Trajectory );
		Assert.Equal( new Vector4( Contact.Position, 0 ), candidates[ 0 ].Point );
		Assert.Equal( new Vector4( Contact.Normal, 0 ), candidates[ 0 ].Direction );
		Assert.Equal( new Vector4( Contact.SurfaceNormal, 0 ), candidates[ 0 ].Axis );
		Assert.Equal( new Vector4( .5f ), candidates[ 0 ].Time );
		Assert.Equal( 30, candidates[ 0 ].SampleIndex );
		Assert.Equal( 6, candidates[ 0 ].Kind );
		Assert.Equal( Contact.Transform, candidates[ 0 ].ContactFrame );
		Assert.Equal( (uint)Contact.Material, candidates[ 0 ].Material );
		Assert.Equal( 123u, candidates[ 0 ].Identity );
		Assert.Equal( 5, candidates[ 0 ].RequestFlag );
	}

	[Fact]
	public void UnchangedAndMissDeliveriesPreserveScoringCorrections()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		var transfer = new GrindCandidateQueryTransfer();
		queries.Targets[ 0 ].Reset( Request, 0 );
		transfer.TrackRequests( queries, candidates );
		queries.Targets[ 0 ].Deliver( Contact );
		transfer.Refresh( queries, candidates );
		candidates[ 0 ].Direction = Vector4.UnitX;
		candidates[ 0 ].Trajectory = candidates[ 0 ].Trajectory with { Velocity = new Vector4( 17 ) };
		var corrected = candidates[ 0 ];
		transfer.Refresh( queries, candidates );
		Assert.Equal( corrected, candidates[ 0 ] );
		queries.Targets[ 0 ].Deliver( TrajectoryContact.Empty );
		transfer.Refresh( queries, candidates );
		Assert.Equal( corrected, candidates[ 0 ] );
		Assert.True( queries.Targets[ 0 ].Completed );
	}

	[Fact]
	public void NewRequestWithIdenticalValuesStillResetsCorrectedCandidate()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		var transfer = new GrindCandidateQueryTransfer();
		queries.Targets[ 0 ].Reset( Request, 0 );
		transfer.TrackRequests( queries, candidates );
		queries.Targets[ 0 ].Deliver( Contact );
		transfer.Refresh( queries, candidates );
		candidates[ 0 ].Trajectory = candidates[ 0 ].Trajectory with { Velocity = new Vector4( 99 ) };
		queries.Targets[ 0 ].Reset( Request, 0 );
		transfer.TrackRequests( queries, candidates );
		Assert.Equal( new Vector4( Request.Velocity, 12 ), candidates[ 0 ].Trajectory.Velocity );
		Assert.Equal( new Vector4( -1 ), candidates[ 0 ].Time );
		Assert.Equal( 0u, candidates[ 0 ].Identity );
		Assert.False( queries.Targets[ 0 ].Completed );
	}

	[Fact]
	public void TrackingAnEmptyFanLeavesPreviousRecordsIntact()
	{
		var queries = new GrindQueryRequestState();
		var candidates = new GrindScoringCandidate[ 7 ];
		candidates[ 0 ].Time = new( 5 );
		new GrindCandidateQueryTransfer().TrackRequests( queries, candidates );
		Assert.Equal( new Vector4( 5 ), candidates[ 0 ].Time );
	}

	[Fact]
	public void LateResultOutsideCurrentCountStillUpdatesItsTrackedSlot()
	{
		var queries = new GrindQueryRequestState { Count = 2 };
		var candidates = new GrindScoringCandidate[ 7 ];
		var transfer = new GrindCandidateQueryTransfer();
		queries.Targets[ 0 ].Reset( Request, 0 );
		queries.Targets[ 1 ].Reset( Request, 0 );
		transfer.TrackRequests( queries, candidates );
		queries.Count = 1;
		queries.Targets[ 1 ].Deliver( Contact );
		transfer.Refresh( queries, candidates );
		Assert.Equal( new Vector4( .5f ), candidates[ 1 ].Time );
		Assert.Equal( 123u, candidates[ 1 ].Identity );
	}

	[Fact]
	public void UntrackedSeedCandidatesRemainCallerOwned()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		candidates[ 0 ].Time = new( 9 );
		queries.Targets[ 0 ].Deliver( Contact );
		new GrindCandidateQueryTransfer().Refresh( queries, candidates );
		Assert.Equal( new Vector4( 9 ), candidates[ 0 ].Time );
	}

	[Fact]
	public void CancelledDeliveryLeavesResetCandidateIncomplete()
	{
		var queries = new GrindQueryRequestState { Count = 1 };
		var candidates = new GrindScoringCandidate[ 7 ];
		var transfer = new GrindCandidateQueryTransfer();
		var service = new TrajectoryQueryService( 7, 7 );
		service.Submit( queries.Targets[ 0 ], Request, 1, 0 );
		transfer.TrackRequests( queries, candidates );
		service.Cancel( 7 );
		var world = new MeshWorldQuery( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
		service.Process( new( world, new( 1f / 60f, .0001f, .7f, 1e-6f ) ), [] );
		transfer.Refresh( queries, candidates );
		Assert.False( queries.Targets[ 0 ].Completed );
		Assert.Equal( new Vector4( -1 ), candidates[ 0 ].Time );
	}
}
