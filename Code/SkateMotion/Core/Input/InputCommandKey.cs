namespace SkateMotion.Core.Input;

internal readonly record struct InputCommandKey( uint A, uint B, uint C, uint D, uint E, uint F )
{
	internal static InputCommandKey Encode( byte[] name ) => Encode( name.AsSpan() );

	internal static InputCommandKey Encode( ReadOnlySpan<byte> name )
	{
		Span<uint> words = stackalloc uint[ 6 ];
		words.Clear();
		for ( var i = 0; i < Math.Min( name.Length, 36 ) && name[ i ] != 0; i++ )
		{
			var character = unchecked( (sbyte)name[ i ] );
			var digit = character >= 97 ? character - 86
				: character <= 57 ? character - 47
				: character <= 90 ? character - 54 : character - 58;
			var weight = 79235168u;
			for ( var position = 0; position < i % 6; position++ )
				weight /= 38;
			words[ i / 6 ] = unchecked( words[ i / 6 ] + (uint)digit * weight );
		}
		return new( words[ 0 ], words[ 1 ], words[ 2 ], words[ 3 ], words[ 4 ], words[ 5 ] );
	}
}
