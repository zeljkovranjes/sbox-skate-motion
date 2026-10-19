namespace SkateMotion.Core.Collision;

internal readonly record struct CollisionMapContactContext
{
	internal ReadOnlyMemory<uint> BodyState { get; init; }
	internal ReadOnlyMemory<uint> BodyFilter { get; init; }
	internal ReadOnlyMemory<uint> TriangleFilter { get; init; }
	internal uint BodyIdentity { get; init; }
	internal uint Category { get; init; }
	internal uint StaticFriction { get; init; }
	internal uint SlidingFriction { get; init; }
	internal uint Restitution { get; init; }
	internal uint MaximumExtension { get; init; }
	internal TriangleContactFilterContext TriangleContext { get; init; }
}
