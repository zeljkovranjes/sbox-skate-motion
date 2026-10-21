using System.Collections.Generic;

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

	private Dictionary<string, (float Start, float End)> _windows;

	/// <summary>The window (fractions of the clip; -1 for all of it) of the first attribute whose name, without dashes or
	/// underscores, is <paramref name="name"/>.</summary>
	internal bool TryWindow( string name, out float start, out float end )
	{
		if ( _windows is null )
		{
			var windows = new Dictionary<string, (float Start, float End)>( StringComparer.Ordinal );
			foreach ( var record in _records )
				windows.TryAdd( ClipFlags.Normalize( record.Name.Decode() ), (record.Start, record.End) );
			_windows = windows;
		}
		var found = _windows.TryGetValue( name, out var window );
		(start, end) = window;
		return found;
	}
}
