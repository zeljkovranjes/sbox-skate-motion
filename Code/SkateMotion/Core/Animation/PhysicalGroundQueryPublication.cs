namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

internal static class PhysicalGroundQueryPublication
{
	internal const int RecordWordCount = 46;

	/// <summary>Publishes a fresh physical query or retains the prior record under the controller's phase gates.</summary>
	/// <returns>The controller's resulting support gate, including the distinct stale-record fallback.</returns>
	internal static bool Update( ReadOnlySpan<uint> owner, int availableResults, Span<uint> retained,
		uint previousAction, bool physicalFallbackActive, Vector4 feedbackPosition, Vector4 fallbackNormal )
	{
		if ( retained.Length < RecordWordCount )
			throw new ArgumentException( "The complete retained query record is required.", nameof( retained ) );
		if ( availableResults > 0 )
		{
			if ( owner.Length < 0x128 / 4 )
				throw new ArgumentException( "The complete query output is required.", nameof( owner ) );
			owner.Slice( 0x70 / 4, RecordWordCount ).CopyTo( retained );
			return (retained[ 0xb0 / 4 ] & 0x30u) == 0x30u;
		}
		if ( previousAction != 501 && physicalFallbackActive )
		{
			retained[ 0xb0 / 4 ] = 1;
			Write( retained, 0, feedbackPosition );
			Write( retained, 4, fallbackNormal );
		}
		return true;
	}

	private static void Write( Span<uint> words, int offset, Vector4 value )
	{
		words[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		words[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		words[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		words[ offset + 3 ] = BitConverter.SingleToUInt32Bits( value.W );
	}
}
