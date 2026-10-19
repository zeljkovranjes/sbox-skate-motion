namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Bounds-checked primitive reads for raw animation containers.</summary>
internal ref struct AnimationBinaryReader
{
	private readonly ReadOnlySpan<byte> _data;
	private int _position;

	internal AnimationBinaryReader( ReadOnlySpan<byte> data ) => _data = data;
	internal int Remaining => _data.Length - _position;

	internal ReadOnlySpan<byte> Take( int length )
	{
		if ( length < 0 || length > Remaining )
			throw new FormatException( "Truncated animation container." );
		var result = _data.Slice( _position, length );
		_position += length;
		return result;
	}

	internal byte Byte() => Take( 1 )[ 0 ];

	internal ushort UInt16()
	{
		var bytes = Take( 2 );
		return (ushort)( bytes[ 0 ] | bytes[ 1 ] << 8 );
	}

	internal uint UInt32()
	{
		var bytes = Take( 4 );
		return (uint)( bytes[ 0 ] | bytes[ 1 ] << 8 | bytes[ 2 ] << 16 | bytes[ 3 ] << 24 );
	}

	internal float Single()
	{
		var value = BitConverter.Int32BitsToSingle( (int)UInt32() );
		if ( !float.IsFinite( value ) )
			throw new FormatException( "Animation values must be finite." );
		return value;
	}

	internal Vector4 Vector() => new( Single(), Single(), Single(), Single() );
	internal string Text() => System.Text.Encoding.UTF8.GetString( Take( Byte() ) );
}
