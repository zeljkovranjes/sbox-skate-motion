namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds approach directions from classified contacts and preserves failed-query output.</summary>
internal sealed class GrindSurfaceDirections
{
	private readonly GrindSurfaceNormal _normal;
	private readonly float _firstAngle;
	private readonly float _secondAngle;
	private readonly float _halfTurn;

	internal GrindSurfaceDirections( LimbSolverMath math, float firstAngle, float secondAngle )
	{
		ArgumentNullException.ThrowIfNull( math );
		_normal = new( math );
		_firstAngle = firstAngle;
		_secondAngle = secondAngle;
		_halfTurn = math.Pi;
	}

	internal GrindSurfaceContact Evaluate( bool querySucceeded, GrindSurfaceClassification surface,
		Vector3 reference, Vector3 point, GrindSurfaceContact previous )
	{
		if ( !querySucceeded )
			return previous with { Kind = GrindSurfaceKind.Blocked, IsFallback = true };
		if ( (surface.Flags & 0x80000000u) != 0 )
			return new( Vector3.UnitY, GrindSurfaceKind.ThinRail, true,
				Vector3.UnitX, Vector3.UnitX, -Vector3.UnitX, Vector3.UnitX );

		var normal = _normal.Evaluate( surface.Up, surface.Direction,
			surface.FirstAngleLimit, surface.SecondAngleLimit );
		var baseSide = Cross( surface.Up, surface.Direction );
		Vector3 side, first, second;
		if ( surface.Kind == GrindSurfaceKind.ThinRail )
		{
			side = baseSide;
			var value = Dot( baseSide, reference - point ) > 0f ? baseSide : -baseSide;
			var axis = Cross( value, surface.Up );
			first = _normal.Rotate( value, axis, _firstAngle );
			second = _normal.Rotate( value, axis, _halfTurn - _secondAngle );
		}
		else
		{
			var axis = Cross( baseSide, surface.Up );
			side = surface.Kind == GrindSurfaceKind.WideRail ? baseSide : Cross( normal, surface.Direction );
			var sign = Dot( baseSide, surface.HighSide ) > 0f ? 1f : -1f;
			second = _normal.Rotate( Scale( baseSide, sign ), Scale( axis, sign ), _secondAngle );
			first = _normal.Rotate( Scale( baseSide, -sign ), Scale( axis, -sign ), _firstAngle );
		}
		return new( normal, surface.Kind, false, side, first, second, surface.HighSide );
	}
}
