namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySamples
{
	private static void CollectEdges( Span<uint> owner, PhysicalGroundQueryResults results,
		LimbSolverMath angles, Span<Vector4> points, Span<bool> found )
	{
		var position = Read( owner, 0 );
		var forward = Read( owner, 16 );
		var up = Read( owner, 32 );
		var cross = Read( owner, 48 );
		var extent = Scale( Abs( cross ), 0.0375f );
		extent = AddScaled( Abs( up ), 1.1f, extent );
		extent = AddScaled( Abs( forward ), 0.9f, extent );
		var center = AddScaled( forward, 0.9f, position );
		center = AddScaled( up, BitConverter.UInt32BitsToSingle( 0x3e999999 ), center );
		var minimum = center - extent;
		var maximum = center + extent;
		Write( owner, 0x4a70, minimum );
		Write( owner, 0x4a80, maximum );
		var edges = results.GetEdges( minimum, maximum, owner[ 0x138 / 4 ], unchecked((int)owner[ 0x6298 / 4 ]) ).Span;
		if ( edges.Length % 12 != 0 || edges.Length > 40 * 12 )
			throw new ArgumentException( "Registered edges require at most forty complete 48-byte records.", nameof( results ) );
		var angleLimit = M.MultiplyAdd( 30f, BitConverter.UInt32BitsToSingle( 0x3c8efa35 ), 0f );
		for ( var index = 0; index < edges.Length / 12; index++ )
		{
			var a = Read( edges, index * 48 );
			var b = Read( edges, index * 48 + 16 );
			if ( MathF.Abs( FoldLineAngle( AngleBetween( b - a, forward, angles ) ) ) > angleLimit )
			{
				if ( IntersectPlane( position, cross, a, b, out var point ) )
				{
					var normal = up;
					Append( owner, point, ref normal, 2 );
				}
				continue;
			}
			var first = Dot( a - position, forward );
			var last = Dot( b - position, forward );
			var crossing = AddScaled( b - a, first / (first - last), a );
			if ( MathF.Abs( Dot( cross, crossing - position ) ) > 0.5f )
				continue;
			var low = MathF.Min( first, last );
			var high = MathF.Max( first, last );
			var inverse = 1f / (last - first);
			for ( var requestIndex = 0; requestIndex < owner[ 0x2a44 / 4 ]; requestIndex++ )
			{
				var request = 0x1550 + requestIndex * 80;
				var distance = Read( owner, request ).Z;
				if ( distance < low || distance > high )
					continue;
				var intersection = AddScaled( b - a, inverse * (distance - first), a );
				var requestPosition = Read( owner, request + 32 );
				var projected = AddScaled( up, Dot( up, intersection - requestPosition ), requestPosition );
				if ( MathF.Abs( Dot( cross, projected - intersection ) ) >= 0.05f )
					continue;
				if ( !found[ requestIndex ] || projected.Y > points[ requestIndex ].Y )
				{
					found[ requestIndex ] = true;
					points[ requestIndex ] = projected;
				}
			}
		}
	}

	private static float AngleBetween( Vector4 first, Vector4 second, LimbSolverMath angles )
	{
		var firstSquared = Dot( first, first );
		var secondSquared = Dot( second, second );
		if ( !(firstSquared > 0.0001f) || !(secondSquared > 0.0001f) )
			return 0f;
		first = NormalizeAngleOnce( first, firstSquared );
		second = NormalizeAngleOnce( second, secondSquared );
		return angles.ArcCosine( Math.Clamp( Dot( first, second ), -1f, 1f ) );
	}

	private static Vector4 NormalizeAngleOnce( Vector4 value, float squared )
	{
		var inverse = 1f / MathF.Sqrt( squared );
		var residual = M.SubtractProduct( squared, M.MultiplyAdd( inverse, inverse, 0f ), 1f );
		inverse = M.MultiplyAdd( M.MultiplyAdd( inverse, 0.5f, 0f ), residual, inverse );
		return Scale( value, inverse );
	}

	private static float FoldLineAngle( float value )
	{
		var turns = M.MultiplyAdd( value, BitConverter.UInt32BitsToSingle( 0x3e22f983 ), 0f );
		var fraction = turns - MathF.Floor( turns );
		if ( fraction > 0.5f )
			fraction -= 1f;
		var wrapped = M.MultiplyAdd( fraction, BitConverter.UInt32BitsToSingle( 0x40c90fdb ), 0f );
		var sign = wrapped <= 0f ? -1f : 1f;
		var magnitude = sign * wrapped;
		if ( magnitude > BitConverter.UInt32BitsToSingle( 0x3fc90fdb ) )
			magnitude -= BitConverter.UInt32BitsToSingle( 0x40490fdb );
		return magnitude * sign;
	}

	private static Vector4 Abs( Vector4 value ) => new( MathF.Abs( value.X ), MathF.Abs( value.Y ), MathF.Abs( value.Z ), MathF.Abs( value.W ) );
}

