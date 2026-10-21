namespace SkateMotion.Core.Animation;

/// <summary>An encoded stream name retaining all six identity words.</summary>
internal readonly record struct AnimationPoseStreamKey( uint A, uint B, uint C, uint D, uint E, uint F )
{
	/// <summary>Encodes a stream name as authored tracks store it, six characters to a word, at most thirty.</summary>
	internal static AnimationPoseStreamKey Encode( string name )
	{
		ArgumentNullException.ThrowIfNull( name );
		if ( name.Length > 30 )
			throw new ArgumentException( "Stream names have at most thirty characters.", nameof( name ) );
		var key = AnimationAttributeKey.Encode( name );
		return new( key.A, key.B, key.C, key.D, key.E, 0 );
	}
}
