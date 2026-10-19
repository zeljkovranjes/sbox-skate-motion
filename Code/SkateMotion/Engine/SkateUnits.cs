namespace SkateMotion.Engine;

using NumericsVector = System.Numerics.Vector3;
using NumericsRotation = System.Numerics.Quaternion;

internal static class SkateUnits
{
	internal const float InchesPerMetre = 39.37007874f;
	internal const float InertiaScale = InchesPerMetre * InchesPerMetre;

	internal static NumericsVector ToCore( Vector3 value ) => new( value.x, value.y, value.z );
	internal static Vector3 ToEngine( NumericsVector value ) => new( value.X, value.Y, value.Z );
	internal static NumericsRotation ToCore( Rotation value ) => new( value.x, value.y, value.z, value.w );
	internal static Rotation ToEngine( NumericsRotation value ) => new( value.X, value.Y, value.Z, value.W );
	internal static NumericsVector Metres( Vector3 value ) => ToCore( value ) / InchesPerMetre;
	internal static Vector3 Inches( NumericsVector value ) => ToEngine( value ) * InchesPerMetre;
}
