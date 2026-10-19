namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Maps an animated segment frame to a solved segment frame using caller-supplied plane hints.</summary>
internal sealed class LimbRotationSolver
{
	private readonly float _minimumAxisLength;
	private readonly float _minimumFrameProduct;

	internal LimbRotationSolver( float minimumAxisLength, float minimumFrameProduct )
	{
		if ( !float.IsFinite( minimumAxisLength ) || minimumAxisLength <= 0f
			|| !float.IsFinite( minimumFrameProduct ) || minimumFrameProduct <= 0f )
			throw new ArgumentException( "Frame thresholds must be finite and positive." );
		_minimumAxisLength = minimumAxisLength;
		_minimumFrameProduct = minimumFrameProduct;
	}

	internal bool TrySolve( Vector3 sourceStart, Vector3 sourceEnd, Vector3 sourceHint,
		Vector3 targetStart, Vector3 targetEnd, Vector3 targetHint, out LimbRigidTransform result )
	{
		result = LimbRigidTransform.Identity;
		if ( !TryCreateFrame( sourceStart, sourceEnd, sourceHint, out var source )
			|| !TryCreateFrame( targetStart, targetEnd, targetHint, out var target ) )
			return false;

		var inverse = new LimbFrame(
			new Vector3( source.X.X, source.Y.X, source.Z.X ),
			new Vector3( source.X.Y, source.Y.Y, source.Z.Y ),
			new Vector3( source.X.Z, source.Y.Z, source.Z.Z ) );
		// Preserve the reverse-axis accumulation for the rigid inverse translation.
		var inverseTranslation = Scale( inverse.Z, 0f - sourceStart.Z );
		inverseTranslation = AddScaled( inverse.Y, 0f - sourceStart.Y, inverseTranslation );
		inverseTranslation = AddScaled( inverse.X, 0f - sourceStart.X, inverseTranslation );
		var rotation = new LimbFrame(
			target.Transform( inverse.X, Vector3.Zero ),
			target.Transform( inverse.Y, Vector3.Zero ),
			target.Transform( inverse.Z, Vector3.Zero ) );
		result = new LimbRigidTransform( rotation, target.Transform( inverseTranslation, targetStart ) );
		return true;
	}

	private bool TryCreateFrame( Vector3 start, Vector3 end, Vector3 hint, out LimbFrame frame )
	{
		var direction = end - start;
		var firstCross = Cross( direction, hint );
		var secondCross = Cross( firstCross, direction );
		var y = NormalizeAxis( direction );
		var x = NormalizeAxis( firstCross );
		var z = NormalizeAxis( secondCross );
		var product = MultiplyAdd( Dot( z, z ), Dot( x, x ), 0f );
		product = MultiplyAdd( product, Dot( y, y ), 0f );
		frame = new LimbFrame( x, y, z );
		return product > _minimumFrameProduct;
	}

	private Vector3 NormalizeAxis( Vector3 value )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _minimumAxisLength ? Scale( value, inverse ) : Vector3.Zero;
	}
}
