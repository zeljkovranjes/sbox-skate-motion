namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static partial class PhysicalGroundQuerySamples
{
	internal static int Append( Span<uint> owner, Vector4 point, ref Vector4 normal,
		uint category, float sortKey = -1f, uint insertion = 0 )
	{
		var projected = Project( owner, point );
		var overhead = projected.Y >= 0.79f && projected.X >= 0f;
		var cross = Read( owner, 48 );
		normal -= Scale( cross, Dot( normal, cross ) );
		var length = Length( normal );
		normal = length < 0.001f ? (projected.Y < 0f ? Read( owner, 16 ) : -Read( owner, 16 ))
			: Scale( normal, M.Reciprocal( length ) );
		int record;
		if ( category == 1 || overhead )
		{
			var count = (int)owner[ 0x3a50 / 4 ];
			if ( count == 64 )
				return -1;
			record = 0x2a50 + count * 64;
			owner[ 0x3a50 / 4 ] = owner[ 0x3a54 / 4 ] = (uint)(count + 1);
		}
		else
		{
			if ( projected.X < 0f )
				return -1;
			var count = (int)owner[ 0x4a60 / 4 ];
			if ( count >= 64 )
				throw new ArgumentException( "Auxiliary sample backing storage is full.", nameof( owner ) );
			record = 0x3a60 + count * 64;
			owner[ 0x4a60 / 4 ] = (uint)(count + 1);
		}
		Write( owner, record, point );
		Write( owner, record + 16, normal );
		Write( owner, record + 32, projected );
		Set( owner, record + 52, sortKey < 0f ? projected.X : sortKey );
		var flags = insertion == 1 ? 1u : insertion == 2 ? 4u : 0u;
		if ( overhead )
			flags |= 8;
		if ( category == 2 )
			flags |= 16;
		owner[ (record + 48) / 4 ] = flags;
		return record;
	}

	private static Vector4 Project( ReadOnlySpan<uint> owner, Vector4 point )
	{
		var delta = point - Read( owner, 0 );
		var horizontal = Dot( Read( owner, 16 ), delta );
		return new( horizontal, Dot( Read( owner, 32 ), delta ), horizontal, horizontal );
	}

	private static bool ConstrainNormal( Vector4 first, Vector4 second, ref Vector4 value )
	{
		first = Normalize( first );
		second = Normalize( second );
		var axis = Normalize( Cross( first, second ) );
		var projected = Normalize( value - Scale( axis, Dot( axis, value ) ) );
		var product = M.MultiplyAdd( Dot( axis, axis ), Dot( projected, projected ), 0f );
		product = M.MultiplyAdd( product, Dot( first, first ), 0f );
		product = M.MultiplyAdd( product, Dot( second, second ), 0f );
		if ( product < 0.1f )
			return false;
		var firstDot = Dot( projected, first );
		var secondDot = Dot( projected, second );
		value = Dot( axis, Cross( first, projected ) ) > 0f && Dot( axis, Cross( projected, second ) ) > 0f
			? projected : firstDot > secondDot ? first : second;
		return true;
	}

	private static bool IntersectPlane( Vector4 origin, Vector4 normal, Vector4 start, Vector4 end, out Vector4 result )
	{
		var first = Dot( normal, start - origin );
		var second = Dot( normal, end - origin );
		result = default;
		if ( first * second >= 0f )
			return false;
		var a = MathF.Abs( first );
		var b = MathF.Abs( second );
		var sum = a + b;
		result = AddScaled( start, a / sum, Scale( end, b / sum ) );
		return true;
	}

	private static Vector4 Normalize( Vector4 value )
	{
		var squared = Dot( value, value );
		var inverse = M.InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : M.MultiplyAdd( squared, inverse, 0f );
		return length > 0.000001f ? Scale( value, inverse ) : Vector4.Zero;
	}

	private static float Float( ReadOnlySpan<uint> words, int offset ) => BitConverter.UInt32BitsToSingle( words[ offset / 4 ] );
	private static void Set( Span<uint> words, int offset, float value ) => words[ offset / 4 ] = BitConverter.SingleToUInt32Bits( value );
	private static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		Float( words, offset ), Float( words, offset + 4 ), Float( words, offset + 8 ), Float( words, offset + 12 ) );
	private static void Write( Span<uint> words, int offset, Vector4 value )
	{
		Set( words, offset, value.X );
		Set( words, offset + 4, value.Y );
		Set( words, offset + 8, value.Z );
		Set( words, offset + 12, value.W );
	}
	private static float Dot( Vector4 left, Vector4 right ) =>
		(M.MultiplyAdd( left.X, right.X, 0f ) + M.MultiplyAdd( left.Y, right.Y, 0f )) + M.MultiplyAdd( left.Z, right.Z, 0f );
	private static float Length( Vector4 value ) => M.SquareRoot( Dot( value, value ) );
	private static Vector4 Scale( Vector4 value, float scale ) => AddScaled( value, scale, Vector4.Zero );
	private static Vector4 AddScaled( Vector4 value, float scale, Vector4 add ) => new(
		M.MultiplyAdd( value.X, scale, add.X ), M.MultiplyAdd( value.Y, scale, add.Y ),
		M.MultiplyAdd( value.Z, scale, add.Z ), M.MultiplyAdd( value.W, scale, add.W ) );
	private static Vector4 Cross( Vector4 left, Vector4 right ) => new(
		M.SubtractProduct( right.Y, left.Z, M.MultiplyAdd( left.Y, right.Z, 0f ) ),
		M.SubtractProduct( right.Z, left.X, M.MultiplyAdd( left.Z, right.X, 0f ) ),
		M.SubtractProduct( right.X, left.Y, M.MultiplyAdd( left.X, right.Y, 0f ) ),
		M.SubtractProduct( right.W, left.W, M.MultiplyAdd( left.W, right.W, 0f ) ) );
}

