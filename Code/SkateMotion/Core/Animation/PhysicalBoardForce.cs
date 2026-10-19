namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A force and local application point retained until the owner's reset phase.</summary>
internal readonly record struct PhysicalBoardForce( uint Kind, Vector3 Force, Vector3 Application );
