using static SkateMotion.Core.Animation.LimbSolverMath;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Classifies seven ordered probe results without performing world collision queries.</summary>
internal sealed class GrindSurfaceClassifier
{
	private readonly LimbSolverMath _math;
	private readonly GrindSurfaceClassificationSettings _settings;

	internal GrindSurfaceClassifier( LimbSolverMath math, GrindSurfaceClassificationSettings settings )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
		_settings = settings;
	}

	/// <summary>Produces the reset invalid result when no investigation was dispatched.</summary>
	internal static GrindSurfaceClassification Inactive( Vector3 center, float reach ) => new(
		center, AddScaled( Vector3.UnitX, reach, center ), center - Scale( Vector3.UnitX, reach ),
		Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, 0f, 0f, GrindSurfaceKind.Blocked, 3u, 1u, 0x80000000u );

	internal GrindSurfaceClassification Classify( GrindSurfaceSection section, GrindSurfaceHit[] hits, uint previousFlags )
	{
		ArgumentNullException.ThrowIfNull( hits );
		if ( hits.Length != 7 )
			throw new ArgumentException( "Seven ordered probe slots are required; absent results use HasHit=false." );
		var kind = ClassifyKind( hits );
		var highSide = Normalize( Cross( Vector3.UnitY, section.Direction ) );
		if ( kind != GrindSurfaceKind.Blocked && !hits[ 0 ].HasHit && hits[ 1 ].HasHit )
			highSide = -highSide;
		var curb = IsCurb( section, hits, kind );
		var flags = UpdateFlags( section, hits, kind, curb, previousFlags );
		var (audio, physical) = SelectMaterials( hits, kind, curb );
		var first = hits[ 2 ].HasHit ? hits[ 2 ].Position : section.FirstFarEnd;
		var second = hits[ 3 ].HasHit ? hits[ 3 ].Position : section.SecondFarEnd;
		var firstAngle = ContactAngle( first - section.Center, section.Up );
		var secondAngle = ContactAngle( second - section.Center, section.Up );
		var firstLimit = (firstAngle + _settings.QuarterTurn) + _settings.AngleClearance;
		var secondLimit = ((_settings.FullTurn - secondAngle) - _settings.QuarterTurn) - _settings.AngleClearance;
		return new( section.Center, first, second, section.Up, section.Direction, highSide,
			firstLimit, secondLimit, kind, audio, physical, flags );
	}

	private GrindSurfaceKind ClassifyKind( GrindSurfaceHit[] hits )
	{
		if ( (hits[ 0 ].HasHit && hits[ 1 ].HasHit) || hits[ 5 ].HasHit )
			return GrindSurfaceKind.Blocked;
		var side = hits[ 0 ].HasHit ? 0 : hits[ 1 ].HasHit ? 1 : -1;
		if ( side < 0 )
			return GrindSurfaceKind.ThinRail;
		var far = hits[ side + 2 ];
		return far.HasHit && far.Fraction < _settings.LedgeFraction ? GrindSurfaceKind.Ledge : GrindSurfaceKind.WideRail;
	}

	private bool IsCurb( GrindSurfaceSection section, GrindSurfaceHit[] hits, GrindSurfaceKind kind )
	{
		if ( kind != GrindSurfaceKind.Ledge || !hits[ 2 ].HasHit || !hits[ 3 ].HasHit )
			return false;
		if ( !(Dot( section.Up, hits[ 2 ].Normal ) > _settings.CurbNormalAlignment)
			|| !(Dot( section.Up, hits[ 3 ].Normal ) > _settings.CurbNormalAlignment) )
			return false;
		var first = Dot( hits[ 2 ].Position - section.Center, section.Up );
		var second = Dot( hits[ 3 ].Position - section.Center, section.Up );
		var high = first;
		var low = second;
		if ( first < second )
		{
			high = second;
			low = first;
		}
		return high > _settings.CurbHighMinimum && high < _settings.CurbHighMaximum
			&& low > _settings.CurbLowMinimum && low < _settings.CurbLowMaximum;
	}

	private uint UpdateFlags( GrindSurfaceSection section, GrindSurfaceHit[] hits,
		GrindSurfaceKind kind, bool curb, uint previousFlags )
	{
		// The blocked bit is sticky; the other five classification bits are replaced.
		var flags = previousFlags & ~0xdc000000u;
		if ( kind == GrindSurfaceKind.Blocked )
			flags |= 0x20000000u;
		if ( curb )
			flags |= 0x40000000u;
		if ( IsStair( hits[ 0 ] ) || IsStair( hits[ 1 ] ) )
			flags |= 0x10000000u;
		if ( TestsOptionalNormal( section, hits[ 6 ] ) )
			flags |= 0x08000000u;
		if ( !hits[ 6 ].HasHit || !(hits[ 6 ].Position.Y - section.Center.Y > -1f) )
			flags |= 0x04000000u;
		return flags;
	}

	private bool TestsOptionalNormal( GrindSurfaceSection section, GrindSurfaceHit hit )
	{
		if ( !hit.HasHit )
			return false;
		var normal = hit.Normal;
		var component = Dot( normal, NormalizeOrZero( section.Direction ) );
		if ( component > 0f )
			normal -= Scale( section.Direction, component );
		normal = NormalizeOrZero( normal );
		if ( !(_settings.OptionalNormalUpLimit > normal.Y) )
			return false;
		var relative = hit.Position - section.Center;
		relative.Y = 0f;
		return Dot( relative, normal ) > 0f;
	}

	private Vector3 NormalizeOrZero( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _settings.MinimumNormalLength ? Scale( value, inverse ) : Vector3.Zero;
	}

	private float ContactAngle( Vector3 delta, Vector3 up )
	{
		// Length followed by reciprocal has different rounding from direct normalization.
		var unit = Scale( delta, Reciprocal( SquareRoot( Dot( delta, delta ) ) ) );
		var cosine = Dot( -up, unit );
		// Ordered vector clamps select the second operand for unordered values.
		// A coincident point therefore reaches cosine=1 instead of propagating NaN.
		cosine = -1f > cosine ? -1f : cosine;
		cosine = cosine < 1f ? cosine : 1f;
		return _math.ArcCosine( cosine );
	}

	private static bool IsStair( GrindSurfaceHit hit ) => hit.HasHit && ((hit.Surface >> 7) & 31u) == 8u;

	private static (uint Audio, uint Physical) SelectMaterials( GrindSurfaceHit[] hits, GrindSurfaceKind kind, bool curb )
	{
		if ( curb )
			return (4u, 2u);
		if ( kind == GrindSurfaceKind.ThinRail )
			return (hits[ 4 ].HasHit ? hits[ 4 ].Surface & 127u : 11u, 4u);
		for ( var i = 0; i < 4; i += 2 )
		{
			var first = hits[ i ];
			var second = hits[ i + 1 ];
			var selected = !second.HasHit || (first.HasHit && first.Position.Y > second.Position.Y) ? first : second;
			if ( selected.HasHit )
				return (selected.Surface & 127u, (selected.Surface >> 7) & 31u);
		}
		return hits[ 4 ].HasHit ? (hits[ 4 ].Surface & 127u, (hits[ 4 ].Surface >> 7) & 31u) : (3u, 1u);
	}
}
