namespace SkateMotion.Core.Animation;

/// <summary>Retains explicit bone kinds and partner indices for posture pose operations.</summary>
internal sealed class AnimationPostureBoneMap
{
	private readonly uint[] _kinds;
	private readonly int[] _pairedBones;

	internal AnimationPostureBoneMap( ReadOnlySpan<uint> kinds, ReadOnlySpan<int> pairedBones )
	{
		if ( kinds.Length != pairedBones.Length )
			throw new ArgumentException( "Posture kinds and partner indices need matching lengths." );
		for ( var bone = 0; bone < pairedBones.Length; bone++ )
			if ( pairedBones[ bone ] < -1 || pairedBones[ bone ] >= kinds.Length )
				throw new ArgumentOutOfRangeException( nameof( pairedBones ) );
		_kinds = kinds.ToArray();
		_pairedBones = pairedBones.ToArray();
	}

	internal int BoneCount => _kinds.Length;
	internal uint Kind( int bone ) => _kinds[ bone ];
	internal int PairedBone( int bone ) => _pairedBones[ bone ];
}
