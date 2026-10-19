namespace SkateMotion.Core.Collision;

internal readonly record struct CollisionMapBodyInput
{
	internal Memory<uint> Shape { get; init; }
	internal ReadOnlyMemory<uint> Transform { get; init; }
	internal ReadOnlyMemory<uint> Bounds { get; init; }
	internal ReadOnlyMemory<uint> BodyState { get; init; }
	internal int ShapeKind { get; init; }
	internal uint ObjectType { get; init; }
	internal uint ShapeIdentity { get; init; }
	internal uint BodyIdentity { get; init; }
	internal uint BaseMargin { get; init; }
	internal uint StaticFriction { get; init; }
	internal uint SlidingFriction { get; init; }
	internal uint Restitution { get; init; }
	internal uint Material { get; init; }
	internal uint Category { get; init; }
	internal bool Disabled { get; init; }
	internal bool Active { get; init; }
}
