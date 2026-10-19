namespace SkateMotion.Core.Collision;

/// <summary>Retains a complete ordered contact stream for one synchronous owner.</summary>
internal sealed class CollisionContactBuffer : ICollisionContactOutput
{
	private uint[] _words = [];
	private int _length;
	internal int Count => _length / 64;
	internal ReadOnlySpan<uint> Records => _words.AsSpan( 0, _length );

	internal void Reset() => _length = 0;

	public void Write( ReadOnlySpan<uint> records )
	{
		if ( records.Length % 64 != 0 )
			throw new ArgumentException( "The stream requires complete contact records.", nameof( records ) );
		var required = checked(_length + records.Length);
		if ( required > _words.Length )
		{
			var capacity = Math.Max( required, checked(Math.Max( _words.Length, 64 ) * 2) );
			Array.Resize( ref _words, capacity );
		}
		records.CopyTo( _words.AsSpan( _length ) );
		_length = required;
	}
}
