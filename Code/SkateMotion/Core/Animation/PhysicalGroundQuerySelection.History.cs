namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySelection
{
	private static void UpdateSlopeHistory( Span<uint> owner, Vector4 direction )
	{
		var squared = M.MultiplyAdd( direction.X, direction.X, 0f ) + M.MultiplyAdd( direction.Z, direction.Z, 0f );
		var horizontal = M.SquareRoot( squared );
		var denominator = MathF.Max( Value( 0x3a83126f ), horizontal );
		var slope = M.MultiplyAdd( 1f / denominator, direction.Y, 0f );
		SetFloat( owner, 0x110, slope );
		var magnitude = MathF.Abs( slope );
		var primary = (owner[ 0x120 / 4 ] & 64u) != 0;
		uint classification = 0;
		// Fixed slope thresholds are the evaluated query angle polynomials.
		if ( magnitude > Value( 0x3f1ff770 ) )
			classification = primary ? slope > 0f ? 2u : 4u : slope > 0f ? 6u : 8u;
		else if ( magnitude > Value( 0x3dd740e4 ) )
			classification = primary ? slope > 0f ? 1u : 3u : slope > 0f ? 5u : 7u;
		owner[ 0x114 / 4 ] = classification;
		if ( classification == 0 )
		{
			owner.Slice( 0x76f4 / 4, 5 ).Clear();
			return;
		}
		if ( owner[ 0x76f8 / 4 ] == classification && owner[ 0x76fc / 4 ] == classification
			&& owner[ 0x7700 / 4 ] == classification )
		{
			owner[ 0x76f4 / 4 ] = classification;
			return;
		}
		owner[ 0x114 / 4 ] = owner[ 0x76f4 / 4 ];
		var next = (owner[ 0x7704 / 4 ] + 1) % 3;
		owner[ 0x7704 / 4 ] = next;
		owner[ 0x76f8 / 4 + (int)next ] = classification;
	}
}
