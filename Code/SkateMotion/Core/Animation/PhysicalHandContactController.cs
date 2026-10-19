namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Projects a retained contact target, contracts its reach and publishes a weighted hand pin.</summary>
internal sealed class PhysicalHandContactController
{
	private const ulong DirectTarget = 1UL << 63;
	private const ulong ProjectedTarget = 1UL << 62;
	private const float StepSeconds = .01666666753590107f;
	private const float PositionEpsilon = .0000152587890625f;
	private const float ProjectionEpsilon = .019999999552965164f;
	private const float MaximumReach = .6499999761581421f;
	private readonly GrindHeightCorrectionCurve _reachCurve;
	private readonly float _fadeInSeconds;
	private readonly float _fadeOutSeconds;
	private readonly float _threshold;
	private readonly float _contraction;

	internal PhysicalHandContactController( GrindHeightCorrectionCurve reachCurve,
		float fadeInSeconds, float fadeOutSeconds, float threshold, float contraction )
	{
		ArgumentNullException.ThrowIfNull( reachCurve );
		_reachCurve = reachCurve;
		_fadeInSeconds = fadeInSeconds;
		_fadeOutSeconds = fadeOutSeconds;
		_threshold = threshold;
		_contraction = contraction;
	}

	internal void Update( PhysicalHandContactState state, EffectorRigState rig, EffectorPoseCache cache,
		bool[] pinning, PhysicalHandContactInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( cache );
		if ( cache.Bones.Length < 11 )
			throw new ArgumentException( "Hand contact requires the canonical cached arm bones." );
		if ( ( input.RequestFlags & ProjectedTarget ) != 0 )
		{
			state.Weight = 0f;
			state.Radius = new Vector4( -1f );
			state.Flags &= ~(DirectTarget | ProjectedTarget);
		}
		var rightHand = ((input.ObjectFlags >> 29) & 1UL) != (input.RequestFlags >> 63);
		var active = AllGreater( input.Metric, -_threshold );
		if ( !active )
			state.Flags |= DirectTarget;
		var anchor = input.ModelToWorld.Transform( cache.Bones[ rightHand ? 7 : 3 ].Translation );
		var endpoint = input.ModelToWorld.Transform( cache.Bones[ rightHand ? 10 : 6 ].Translation );
		var target = ( state.Flags & DirectTarget ) != 0
			? input.Point : Project( state, input.Point, anchor, endpoint );
		target = BoundToEndpoint( target, endpoint );

		var step = active ? StepSeconds / _fadeInSeconds : -StepSeconds / _fadeOutSeconds;
		var weight = state.Weight + step;
		weight = -weight >= 0f ? 0f : weight;
		state.Weight = 1f - weight >= 0f ? weight : 1f;
		if ( state.Weight <= 0f )
			return;
		PhysicalHandPinController.Update( rig, pinning, rightHand, input.ModelToWorld,
			target, MaximumReach, state.Weight );
	}

	private Vector3 Project( PhysicalHandContactState state, Vector3 point, Vector3 anchor, Vector3 endpoint )
	{
		var along = endpoint - point;
		if ( !(Length( along ) > PositionEpsilon) )
			return anchor;
		var direction = Normalize( along );
		var projection = Dot( anchor - point, direction );
		var target = anchor;
		if ( 0f >= projection )
		{
			state.Flags |= DirectTarget;
		}
		else
		{
			var projected = AddScaled( direction, projection, point );
			target = projected;
			if ( ( state.Flags & ProjectedTarget ) == 0 )
			{
				var lateral = anchor - projected;
				var limit = _reachCurve.Evaluate( projection );
				target = Length( lateral ) > limit ? AddScaled( Normalize( lateral ), limit, projected ) : anchor;
			}
		}
		target = Contract( state, point, target );
		if ( ( state.Flags & DirectTarget ) != 0 )
			return point;
		if ( ( state.Flags & ProjectedTarget ) == 0 )
		{
			var closest = AddScaled( direction, Dot( target - point, direction ), point );
			if ( ProjectionEpsilon > Length( target - closest ) )
				state.Flags |= ProjectedTarget;
		}
		return target;
	}

	private Vector3 Contract( PhysicalHandContactState state, Vector3 point, Vector3 target )
	{
		var offset = target - point;
		var distance = Length( offset );
		var radius = state.Radius;
		if ( AllLess( radius, 0f ) )
			radius = new Vector4( distance );
		radius -= new Vector4( _contraction );
		radius = new Vector4( MathF.Min( distance, radius.X ), MathF.Min( distance, radius.Y ),
			MathF.Min( distance, radius.Z ), MathF.Min( distance, radius.W ) );
		state.Radius = radius;
		if ( AllLess( radius, PositionEpsilon ) || PositionEpsilon > distance )
		{
			state.Flags |= DirectTarget;
			return target;
		}
		var direction = Normalize( offset );
		return new Vector3( MultiplyAdd( direction.X, radius.X, point.X ),
			MultiplyAdd( direction.Y, radius.Y, point.Y ), MultiplyAdd( direction.Z, radius.Z, point.Z ) );
	}

	private static Vector3 BoundToEndpoint( Vector3 target, Vector3 endpoint )
	{
		var offset = target - endpoint;
		return Length( offset ) > MaximumReach ? AddScaled( Normalize( offset ), MaximumReach, endpoint ) : target;
	}

	private static float Length( Vector3 value ) => SquareRoot( Dot( value, value ) );

	private static bool AllGreater( Vector4 value, float threshold ) =>
		value.X > threshold && value.Y > threshold && value.Z > threshold && value.W > threshold;

	private static bool AllLess( Vector4 value, float threshold ) =>
		value.X < threshold && value.Y < threshold && value.Z < threshold && value.W < threshold;
}
