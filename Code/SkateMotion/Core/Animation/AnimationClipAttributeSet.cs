namespace SkateMotion.Core.Animation;

/// <summary>Immutable clip identity and ordered attribute records, including an explicitly empty table.</summary>
internal sealed class AnimationClipAttributeSet
{
	private readonly uint[] _nameWords;
	private readonly AnimationClipAttribute[] _records;

	internal AnimationClipAttributeSet( string name, uint[] nameWords, AnimationClipAttribute[] records )
	{
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( nameWords );
		ArgumentNullException.ThrowIfNull( records );
		if ( nameWords.Length != 6 )
			throw new ArgumentException( "Clip identities need six words.", nameof( nameWords ) );
		Name = name;
		_nameWords = new uint[ 6 ];
		Array.Copy( nameWords, _nameWords, 6 );
		_records = new AnimationClipAttribute[ records.Length ];
		for ( var index = 0; index < records.Length; index++ )
		{
			ArgumentNullException.ThrowIfNull( records[ index ] );
			_records[ index ] = records[ index ];
		}
	}

	internal string Name { get; }
	internal ReadOnlySpan<uint> NameWords => _nameWords;
	internal ReadOnlySpan<AnimationClipAttribute> Records => _records;
}
