using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Builds the ordered collision cross-section used to classify a sampled edge.</summary>
internal sealed class GrindSurfaceProbePlan
{
	private readonly GrindSurfaceProbeSettings _settings;
	private readonly GrindSurfaceProbe[] _probes = new GrindSurfaceProbe[ 7 ];
	internal Vector3 Center { get; private set; }
	internal Vector3 Up { get; private set; }
	internal Vector3 Direction { get; private set; }
	internal int Count { get; private set; }

	internal GrindSurfaceProbePlan( GrindSurfaceProbeSettings settings )
	{
		_settings = settings;
	}

	internal GrindSurfaceProbe GetProbe( int index )
	{
		if ( index < 0 || index >= Count )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		return _probes[ index ];
	}

	internal void Build( RailSegment edge, Vector3 reference, float reach,
		bool includeDownward, Vector3 downwardOrigin )
	{
		Count = 0;
		Center = reference;
		var difference = edge.End - edge.Start;
		var up = LimbSolverMath.Cross( difference, LimbSolverMath.Cross( Vector3.UnitY, difference ) );
		var upLength = LimbSolverMath.SquareRoot( LimbSolverMath.Dot( up, up ) );
		var length = LimbSolverMath.SquareRoot( LimbSolverMath.Dot( difference, difference ) );
		Direction = length > _settings.MinimumNormalizeLength ? LimbSolverMath.Normalize( difference ) : difference;
		Up = upLength > _settings.MinimumNormalizeLength ? LimbSolverMath.Normalize( up ) : up;
		if ( _settings.MinimumUpLength > upLength )
			return;

		var projection = LimbSolverMath.AddScaled( Direction,
			LimbSolverMath.Dot( reference - edge.Start, Direction ), edge.Start );
		// Keep the two subtractions: cancelling a large reference changes rounding.
		Center = reference - (reference - projection);
		var side = LimbSolverMath.Cross( Up, Direction );
		var narrow = LimbSolverMath.Scale( side, _settings.NearWidth );
		var high = LimbSolverMath.Scale( Up, _settings.NearHeight );
		var wide = LimbSolverMath.Scale( side, reach );
		var deep = LimbSolverMath.Scale( Up, reach * _settings.FarHeightScale );
		var overhead = LimbSolverMath.Scale( Up, _settings.NearWidth );
		_probes[ 0 ] = new( (Center + narrow) + high, (Center + narrow) - high, 0f );
		_probes[ 1 ] = new( (Center - narrow) + high, (Center - narrow) - high, 0f );
		_probes[ 2 ] = new( (Center + wide) + deep, (Center + wide) - deep, 0f );
		_probes[ 3 ] = new( (Center - wide) + deep, (Center - wide) - deep, 0f );
		_probes[ 4 ] = new( Center + high, Center - high, 0f );
		_probes[ 5 ] = new( (Center + overhead) + narrow, (Center + overhead) - narrow, _settings.OverheadRadius );
		Count = 6;
		if ( includeDownward )
		{
			_probes[ 6 ] = new( downwardOrigin, downwardOrigin + new Vector3( 0f, -_settings.DownwardDepth, 0f ), 0f );
			Count = 7;
		}
	}
}
