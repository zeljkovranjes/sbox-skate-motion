namespace SkateMotion.Core.Animation;

/// <summary>Six unmodified value words, retaining encoded names and floating-point bit patterns.</summary>
internal readonly record struct AnimationAttributePayload( uint A, uint B, uint C, uint D, uint E, uint F )
{
	internal float Scalar => BitConverter.UInt32BitsToSingle( A );
}
