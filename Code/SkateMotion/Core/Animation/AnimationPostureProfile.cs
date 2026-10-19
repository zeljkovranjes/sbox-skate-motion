namespace SkateMotion.Core.Animation;

/// <summary>Resolved posture data bound to one ordered clip skeleton.</summary>
internal sealed class AnimationPostureProfile
{
	/// <summary>Container version; currently one.</summary>
	public int Version { get; set; }
	/// <summary>Clip bone names in their original order.</summary>
	public string[] BoneNames { get; set; }
	/// <summary>Parent indices used to validate skeleton identity.</summary>
	public int[] Parents { get; set; }
	/// <summary>Authored body transformation categories.</summary>
	public uint[] BoneKinds { get; set; }
	/// <summary>Partner indices; minus one leaves a bone unchanged.</summary>
	public int[] PairedBones { get; set; }
	/// <summary>Five-word keys searched in authored order.</summary>
	public uint[][] AttributeNames { get; set; }
	/// <summary>Resolved output keys, including values for unpaired entries.</summary>
	public uint[][] MappedAttributes { get; set; }
	/// <summary>Resolved value used when no input key matches.</summary>
	public uint[] MissingAttribute { get; set; }

	internal AnimationPostureTreeProvider CreateProvider( ClipSkeleton skeleton )
	{
		ArgumentNullException.ThrowIfNull( skeleton );
		if ( Version != 1 )
			throw new FormatException( "Unsupported posture profile version." );
		if ( BoneNames is null || Parents is null || BoneKinds is null || PairedBones is null
			|| BoneNames.Length != skeleton.Count || Parents.Length != skeleton.Count
			|| BoneKinds.Length != skeleton.Count || PairedBones.Length != skeleton.Count )
			throw new FormatException( "Posture data must match the complete clip skeleton." );
		for ( var bone = 0; bone < skeleton.Count; bone++ )
			if ( BoneNames[ bone ] != skeleton.Names[ bone ] || Parents[ bone ] != skeleton.Parents[ bone ] )
				throw new FormatException( "Posture data names a different skeleton or bone order." );
		if ( AttributeNames is null || MappedAttributes is null || AttributeNames.Length != MappedAttributes.Length )
			throw new FormatException( "Posture attributes require one resolved value per name." );
		var names = new AnimationAttributeKey[ AttributeNames.Length ];
		var mapped = new AnimationAttributeKey[ names.Length ];
		for ( var i = 0; i < names.Length; i++ )
		{
			names[ i ] = Key( AttributeNames[ i ] );
			mapped[ i ] = Key( MappedAttributes[ i ] );
		}
		return new( new( BoneKinds, PairedBones ), new( names, mapped, Key( MissingAttribute ) ) );
	}

	private static AnimationAttributeKey Key( uint[] words )
	{
		if ( words is null || words.Length != 5 )
			throw new FormatException( "Posture attribute keys require exactly five words." );
		return new( words[ 0 ], words[ 1 ], words[ 2 ], words[ 3 ], words[ 4 ] );
	}
}
