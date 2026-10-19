namespace SkateMotion.Core.Animation;

/// <summary>Provides explicitly loaded contact metadata without synthesized identifiers.</summary>
internal sealed class AnimationContactMetadataTable
{
	private readonly int[] _indices;
	private readonly int[] _kinds;
	private readonly AnimationMovementContactKey[] _keys;

	internal AnimationContactMetadataTable( int[] indices,
		AnimationMovementContactKey[] keys, int[] kinds )
	{
		ArgumentNullException.ThrowIfNull( indices );
		ArgumentNullException.ThrowIfNull( keys );
		ArgumentNullException.ThrowIfNull( kinds );
		if ( indices.Length != 384 || keys.Length != 384 || kinds.Length != 384 )
			throw new ArgumentException( "Contact metadata requires three complete tables of 384 records." );
		_indices = indices.AsSpan().ToArray();
		_keys = keys.AsSpan().ToArray();
		_kinds = kinds.AsSpan().ToArray();
	}

	internal int Index( AnimationContactDescriptor descriptor ) => _indices[ descriptor.TableIndex ];
	internal int Kind( AnimationContactDescriptor descriptor ) => _kinds[ descriptor.TableIndex ];
	internal AnimationMovementContactKey Key( AnimationContactDescriptor descriptor ) => _keys[ descriptor.TableIndex ];
}
