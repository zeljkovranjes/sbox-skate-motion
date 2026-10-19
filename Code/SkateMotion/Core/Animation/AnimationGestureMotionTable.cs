using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Builds a motion identity from configured base names and explicit mode and phase suffixes.</summary>
internal sealed class AnimationGestureMotionTable
{
	private readonly InputCommandKey[] _names;
	private readonly byte[][] _modes;
	private readonly byte[][] _phases;

	internal AnimationGestureMotionTable( InputCommandKey[] names, byte[][] modes, byte[][] phases )
	{
		ArgumentNullException.ThrowIfNull( names );
		_names = names.AsSpan().ToArray();
		_modes = Copy( modes );
		_phases = Copy( phases );
	}

	internal InputCommandKey Build( int configuration, int mode, int phase )
	{
		if ( (uint)configuration >= _names.Length || (uint)mode >= 3 || (uint)phase >= 3 )
			throw new ArgumentOutOfRangeException( nameof( configuration ), "Motion configuration, mode and phase must be supplied by the current actor." );
		var key = _names[ configuration ];
		Span<uint> words = stackalloc uint[] { key.A, key.B, key.C, key.D, key.E, key.F };
		Span<byte> name = stackalloc byte[ 36 ];
		for ( var word = 0; word < 6; word++ )
		{
			var value = words[ word ];
			for ( var digitIndex = 5; digitIndex >= 0; digitIndex-- )
			{
				var digit = value % 38;
				name[ word * 6 + digitIndex ] = digit == 0 ? (byte)0 : digit <= 10 ? (byte)(digit + 47)
					: digit == 37 ? (byte)95 : (byte)(digit + 54);
				value /= 38;
			}
		}
		var length = 0;
		while ( length < name.Length && name[ length ] != 0 )
			length++;
		Append( name, ref length, (byte)'_' );
		Append( name, ref length, _modes[ mode ] );
		Append( name, ref length, (byte)'_' );
		Append( name, ref length, _phases[ phase ] );
		return InputCommandKey.Encode( name[ ..length ] );
	}

	private static void Append( Span<byte> destination, ref int count, byte value )
	{
		if ( count < destination.Length )
			destination[ count++ ] = value;
	}

	private static void Append( Span<byte> destination, ref int count, byte[] source )
	{
		foreach ( var value in source )
		{
			if ( value == 0 )
				break;
			Append( destination, ref count, value );
		}
	}

	private static byte[][] Copy( byte[][] values )
	{
		ArgumentNullException.ThrowIfNull( values );
		if ( values.Length != 3 )
			throw new ArgumentException( "Three explicit suffixes are required.", nameof( values ) );
		var result = new byte[ 3 ][];
		for ( var index = 0; index < result.Length; index++ )
		{
			ArgumentNullException.ThrowIfNull( values[ index ] );
			result[ index ] = values[ index ].AsSpan().ToArray();
		}
		return result;
	}
}
