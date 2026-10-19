namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Prepares the complete board query batch and preserves delayed result caches.</summary>
internal static class PhysicalBoardProbeProducer
{
	internal static void Update( PhysicalBoardProbeState state, PhysicalBoardProbeInput input,
		ReadOnlySpan<PhysicalBodyBinding> bodies, int excludedActor, IPhysicalBoardProbeSink sink )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( sink );
		if ( bodies.Length < 7 )
			throw new ArgumentException( "Seven body bindings are required.", nameof( bodies ) );
		for ( var i = 0; i < 4; i++ )
			ArgumentNullException.ThrowIfNull( bodies[ i ] );
		ArgumentNullException.ThrowIfNull( bodies[ 6 ] );

		PrepareForward( state, input );
		state.SkeletonFlags = (state.SkeletonFlags & ~(1UL << 61))
			| (state.ForwardEnabled ? 1UL << 61 : 0UL);
		sink.Begin();
		for ( var i = 0; i < 4; i++ )
		{
			var start = bodies[ i ].Read().Translation;
			sink.Add( new( start, start - Scale( input.WheelDirection, .2f ), 0f, excludedActor, 3, 0, 0 ) );
		}

		var board = bodies[ 6 ].Read().Translation;
		state.Ground.Origin = board;
		state.Ground.Valid = false;
		sink.Add( new( board, board + new Vector3( 0f, -100f, 0f ), .15f, excludedActor, 3, 0, 0 ) );
		if ( state.ForwardEnabled )
		{
			state.Forward.Origin = state.ForwardStart;
			state.Forward.Valid = false;
			sink.Add( new( state.ForwardStart, state.ForwardEnd, 0f, excludedActor, 3, 0, 0 ) );
		}
		else
		{
			state.Forward.Clear();
		}
		sink.End();
	}

	private static void PrepareForward( PhysicalBoardProbeState state, PhysicalBoardProbeInput input )
	{
		state.ForwardStart = state.ForwardEnd = Vector3.Zero;
		state.ForwardEnabled = (uint)(input.PreviousMode - 100) <= 1
			&& .5f > MathF.Abs( input.Heading.Y )
			&& Dot( input.SurfaceNormal, input.Heading ) > .71f
			&& input.ContactStrength > .01f;
		if ( !state.ForwardEnabled )
			return;

		state.ForwardStart = AddScaled( input.Heading, .05f, input.BoardPosition );
		var perpendicular = Cross( Cross( Vector3.UnitY, input.Heading ), input.Heading );
		var squared = Dot( perpendicular, perpendicular );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		var direction = length > 1e-6f ? Scale( perpendicular, inverse ) : Vector3.Zero;
		state.ForwardEnd = AddScaled( direction, 4f, state.ForwardStart );
	}
}
