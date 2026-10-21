namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>Changes Z-up physics coordinates to the Y-up edge lookup frame without changing units.</summary>
internal static class RailEdgeSpace
{
	// The proper cyclic permutation maps forward X to Z, left Y to X, and up Z to Y.
	internal static Vector3 ToLookup( Vector3 value ) => new( value.Y, value.Z, value.X );

	internal static Vector3 ToWorld( Vector3 value ) => new( value.Z, value.X, value.Y );

	internal static RailSegment ToLookup( RailSegment value ) =>
		value with { Start = ToLookup( value.Start ), End = ToLookup( value.End ), Top = ToLookup( value.Top ) };

	internal static RailSegment ToWorld( RailSegment value ) =>
		value with { Start = ToWorld( value.Start ), End = ToWorld( value.End ), Top = ToWorld( value.Top ) };
}
