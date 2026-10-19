namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>Changes Z-up physics coordinates to the Y-up edge lookup frame without changing units.</summary>
internal static class RailEdgeSpace
{
	// The proper cyclic permutation maps forward X to Z, left Y to X, and up Z to Y.
	internal static Vector3 ToLookup( Vector3 value ) => new( value.Y, value.Z, value.X );

	internal static Vector3 ToWorld( Vector3 value ) => new( value.Z, value.X, value.Y );

	internal static RailSegment ToLookup( RailSegment value ) => new( ToLookup( value.Start ), ToLookup( value.End ) );

	internal static RailSegment ToWorld( RailSegment value ) => new( ToWorld( value.Start ), ToWorld( value.End ) );
}
