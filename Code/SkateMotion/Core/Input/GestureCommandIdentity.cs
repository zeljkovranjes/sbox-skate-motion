namespace SkateMotion.Core.Input;

/// <summary>A profile name kept alongside its encoded action identifier.</summary>
internal sealed class GestureCommandIdentity
{
	internal string Name { get; }
	internal InputCommandKey Key { get; }

	internal GestureCommandIdentity( string name )
	{
		ArgumentNullException.ThrowIfNull( name );
		if ( name.Length == 0 || name.Length > 31 )
			throw new ArgumentOutOfRangeException( nameof( name ) );
		Span<byte> bytes = stackalloc byte[ name.Length ];
		for ( var index = 0; index < name.Length; index++ )
		{
			if ( name[ index ] == 0 || name[ index ] > 127 )
				throw new ArgumentException( "Gesture names must be nonzero ASCII.", nameof( name ) );
			bytes[ index ] = (byte)name[ index ];
		}
		Name = name;
		Key = InputCommandKey.Encode( bytes );
	}

	internal bool Matches( GestureCommandIdentity other )
	{
		if ( other is null || Name.Length != other.Name.Length )
			return false;
		for ( var index = 0; index < Name.Length; index++ )
		{
			var left = Fold( Name[ index ] );
			var right = Fold( other.Name[ index ] );
			if ( left != right )
				return false;
		}
		return true;
	}

	private static char Fold( char value ) => value is >= 'A' and <= 'Z' ? (char)(value + 32) : value;
}
