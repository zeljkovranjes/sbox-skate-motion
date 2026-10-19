using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Consumes ranked samples through map investigation, rejection gates and accepted-contact output.</summary>
internal sealed class GrindCandidateAcceptance
{
	private readonly GrindEdgeContactSampler _sampler;
	private readonly GrindEdgeSampleQueue _queue;
	private readonly GrindSurfaceContactQuery _contacts;
	private readonly GrindContactCorrection _correction;
	private readonly GrindCandidateNormal _normal;
	private readonly LimbSignedAngle _angle;
	private readonly GrindCandidateSettings _settings;
	internal int QueryCount { get; private set; }
	internal int RemainingCount => _queue.RemainingCount;
	internal int SourceEdgeCount => _queue.SourceEdgeCount;

	internal GrindCandidateAcceptance( GrindEdgeContactSampler sampler, GrindEdgeSampleQueue queue,
		GrindSurfaceContactQuery contacts, GrindContactCorrection correction, GrindCandidateNormal normal,
		LimbSignedAngle angle, GrindCandidateSettings settings )
	{
		ArgumentNullException.ThrowIfNull( sampler );
		ArgumentNullException.ThrowIfNull( queue );
		ArgumentNullException.ThrowIfNull( contacts );
		ArgumentNullException.ThrowIfNull( correction );
		ArgumentNullException.ThrowIfNull( normal );
		ArgumentNullException.ThrowIfNull( angle );
		_sampler = sampler;
		_queue = queue;
		_contacts = contacts;
		_correction = correction;
		_normal = normal;
		_angle = angle;
		_settings = settings;
	}

	internal bool TryAccept( GrindEdgeRecord[] edges, int count, GrindCandidateInput input,
		MeshWorldQuery query, MeshQueryInstance[] instances, ref GrindCandidateState state )
	{
		var trajectory = state.Trajectory;
		_queue.Populate( _sampler, trajectory, edges, count );
		QueryCount = 0;
		var predicted = trajectory.Position + Scale( trajectory.Velocity, state.Time );
		predicted += Scale( Scale( trajectory.Acceleration, .5f ), MultiplyAdd( state.Time, state.Time, 0f ) );
		var sample = default( GrindEdgeSample );
		while ( _queue.Next( input.SeparationThreshold, ref sample ) )
		{
			if ( sample.Point.Y - predicted.Y < _settings.MinimumHeightDelta
				|| sample.Direction.Y > _settings.MaximumDirectionY )
				continue;
			state.Edge = edges[ sample.EdgeIndex ].Segment;
			if ( !ApproachPasses( input.Approach, state.Edge.End - state.Edge.Start, out var horizontalSquared ) )
				continue;
			QueryCount++;
			state.Contact = _contacts.Evaluate( query, instances, state.Edge,
				input.Reference, sample.Point, input.Reach, input.Filter, state.Contact );
			if ( state.Contact.Kind == GrindSurfaceKind.Blocked )
				continue;
			state.IsLedge = state.Contact.Kind == GrindSurfaceKind.Ledge;
			state.Side = state.Contact.Side;
			state.HighSide = state.Contact.HighSide;
			var horizontalLimit = state.IsLedge ? _settings.MaximumLedgeHorizontalSquared : _settings.MaximumRailHorizontalSquared;
			if ( horizontalLimit < horizontalSquared )
				continue;
			var threshold = SeparationLimit( input, sample, state );
			if ( !(sample.Separation < threshold) )
				continue;
			var displacement = CorrectionDisplacement( input, sample, state.Edge.Start );
			if ( !CorrectionAnglePasses( trajectory.Velocity, displacement, sample.Time ) )
				continue;
			var velocity = _correction.Apply( new Vector4( trajectory.Velocity, 0f ),
				new Vector4( displacement, 0f ), sample.SampleIndex, _settings.MaximumCorrectionSpeed );
			state.Trajectory = trajectory with { Velocity = new Vector3( velocity.X, velocity.Y, velocity.Z ) };
			state.SampleIndex = sample.SampleIndex;
			state.Time = MultiplyAdd( (float)sample.SampleIndex, _settings.SampleInterval, 0f );
			state.Normal = _normal.Evaluate( state.Contact.Normal, sample.Direction, trajectory.Velocity,
				_settings.VelocityGain, _settings.MaximumTiltDegrees );
			state.EdgeUp = _normal.EdgeUp( sample.Direction );
			state.IsActive = true;
			return true;
		}
		return false;
	}

	private bool ApproachPasses( Vector3 approach, Vector3 difference, out float horizontalSquared )
	{
		var squared = Dot( difference, difference );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		var direction = length > _settings.MinimumDirectionLength ? Scale( difference, inverse ) : Vector3.Zero;
		var perpendicular = approach - Scale( direction, Dot( direction, approach ) );
		var vertical = MathF.Abs( MultiplyAdd( perpendicular.Y, 1f - MathF.Abs( direction.Y ), 0f ) );
		perpendicular.Y = 0f;
		horizontalSquared = Dot( perpendicular, perpendicular );
		return !(vertical > _settings.MaximumVerticalApproach);
	}

	private float SeparationLimit( GrindCandidateInput input, GrindEdgeSample sample, GrindCandidateState state )
	{
		if ( !state.IsLedge )
			return input.SeparationThreshold;
		var positiveCorrection = Dot( sample.TrajectoryPoint - sample.Point, state.Contact.HighSide ) > 0f;
		var negativeSupport = -Dot( input.Support - state.Edge.Start, state.Contact.HighSide ) >= 0f;
		var scale = positiveCorrection
			? (negativeSupport ? _settings.PositiveCorrectionNegativeSupportScale : _settings.PositiveCorrectionPositiveSupportScale)
			: (negativeSupport ? _settings.NegativeCorrectionNegativeSupportScale : _settings.NegativeCorrectionPositiveSupportScale);
		return CollisionScalarMath.Narrow( CollisionScalarMath.Product(
			CollisionScalarMath.Widen( scale ), CollisionScalarMath.Widen( input.SeparationThreshold ) ) );
	}

	private Vector3 CorrectionDisplacement( GrindCandidateInput input, GrindEdgeSample sample, Vector3 endpoint )
	{
		var relative = sample.TrajectoryPoint - endpoint;
		var displacement = -(relative - Scale( sample.Direction, Dot( sample.Direction, relative ) ));
		var length = SquareRoot( Dot( displacement, displacement ) );
		var scale = 1f;
		if ( !(input.SeparationThreshold > length) )
		{
			var second = CollisionScalarMath.Product( CollisionScalarMath.Widen( input.SecondDeckExtent ), .5d );
			var width = CollisionScalarMath.MultiplyAdd( CollisionScalarMath.Widen( input.FirstDeckExtent ), .5d, second );
			var remaining = -CollisionScalarMath.MultiplyAdd( width,
				CollisionScalarMath.Widen( _settings.LongCorrectionScale ), -CollisionScalarMath.Widen( length ) );
			scale = CollisionScalarMath.Narrow( CollisionScalarMath.Divide( remaining, CollisionScalarMath.Widen( length ) ) );
		}
		return Scale( displacement, scale );
	}

	private bool CorrectionAnglePasses( Vector3 velocity, Vector3 displacement, float time )
	{
		var candidate = AddScaled( displacement, Reciprocal( time ), velocity );
		var product = MultiplyAdd( Dot( velocity, velocity ), Dot( candidate, candidate ), 0f );
		var angle = product > _settings.MinimumAngleLengthProduct
			? _angle.Evaluate( velocity, candidate, Vector3.Zero ) : 0f;
		var limit = MultiplyAdd( _settings.RadiansPerDegree, _settings.MaximumCorrectionAngleDegrees, 0f );
		return !(angle > limit);
	}
}
