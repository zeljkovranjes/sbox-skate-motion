namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>A steep surface ahead of a board in the air: where its travel meets it, its normal, and how high (m) that is
/// above the floor below.</summary>
internal readonly record struct WallSight( Vector3 Point, Vector3 Normal, float Height );
