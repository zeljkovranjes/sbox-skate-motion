namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using M = LimbSolverMath;

internal static class PhysicalGroundQueryCandidateWriter
{
	/// <summary>Appends an admitted point using its physical displacement as the candidate direction.</summary>
	internal static int AppendPoint( Span<uint> owner, Vector4 point, Vector4 normal,
		int segmentIndex, uint flags, uint kind )
	{
		Validate( owner, segmentIndex );
		var delta = point - Read( owner, 0 );
		var length = M.SquareRoot( Dot( delta, delta ) );
		var direction = length < BitConverter.UInt32BitsToSingle( 0x3ca3d70a ) ? Read( owner, 16 ) : delta;
		return Append( owner, point, normal, direction, segmentIndex, flags, kind );
	}

	/// <summary>Writes an admitted candidate and its projected interval, preserving record padding.</summary>
	internal static int Append( Span<uint> owner, Vector4 point, Vector4 normal, Vector4 direction,
		int segmentIndex, uint flags, uint kind )
	{
		Validate( owner, segmentIndex );
		var index = (int)owner[ 0x76f0 / 4 ];
		var offset = 0x62a0 + index * 80;
		owner[ 0x76f0 / 4 ] = (uint)(index + 1);
		Write( owner, offset, point );
		Write( owner, offset + 16, normal );
		Write( owner, offset + 32, direction );
		owner[ (offset + 48) / 4 ] = flags;
		owner[ (offset + 64) / 4 ] = unchecked((uint)segmentIndex);
		owner[ (offset + 68) / 4 ] = kind;
		var position = Read( owner, 0 );
		var axis = Read( owner, 16 );
		var projection = Dot( axis, point - position );
		SetFloat( owner, offset + 52, projection );
		SetFloat( owner, offset + 56, projection );
		SetFloat( owner, offset + 60, projection );
		if ( segmentIndex < 0 )
			SetFloat( owner, offset + 52, 0f );
		else
		{
			var segment = 0x4a90 + segmentIndex * 96;
			var category = owner[ segment / 4 ];
			if ( category != 0 )
			{
				var endpoint = Read( owner, segment + (category == 2 ? 16 : 32) );
				var extent = Dot( axis, endpoint - position );
				SetFloat( owner, offset + (projection > extent ? 52 : 56), extent );
			}
		}
		return index;
	}

	private static void Validate( ReadOnlySpan<uint> owner, int segmentIndex )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount )
			throw new ArgumentException( "The complete retained query owner is required.", nameof( owner ) );
		if ( owner[ 0x76f0 / 4 ] >= 64 )
			throw new ArgumentException( "The candidate backing storage is full.", nameof( owner ) );
		if ( segmentIndex >= 64 || segmentIndex >= 0 && segmentIndex >= owner[ 0x6294 / 4 ] )
			throw new ArgumentOutOfRangeException( nameof( segmentIndex ) );
	}

	private static float Float( ReadOnlySpan<uint> words, int offset ) => BitConverter.UInt32BitsToSingle( words[ offset / 4 ] );
	private static void SetFloat( Span<uint> words, int offset, float value ) => words[ offset / 4 ] = BitConverter.SingleToUInt32Bits( value );
	private static Vector4 Read( ReadOnlySpan<uint> words, int offset ) => new(
		Float( words, offset ), Float( words, offset + 4 ), Float( words, offset + 8 ), Float( words, offset + 12 ) );
	private static void Write( Span<uint> words, int offset, Vector4 value )
	{
		SetFloat( words, offset, value.X );
		SetFloat( words, offset + 4, value.Y );
		SetFloat( words, offset + 8, value.Z );
		SetFloat( words, offset + 12, value.W );
	}
	private static float Dot( Vector4 left, Vector4 right ) =>
		(M.MultiplyAdd( left.X, right.X, 0f ) + M.MultiplyAdd( left.Y, right.Y, 0f )) + M.MultiplyAdd( left.Z, right.Z, 0f );
}
