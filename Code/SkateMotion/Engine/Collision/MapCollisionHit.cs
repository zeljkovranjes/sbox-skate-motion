namespace SkateMotion.Engine.Collision;

using Sandbox;
using NumericsVector = System.Numerics.Vector3;

/// <summary>Map query output in Z-up metres with the selected engine shape.</summary>
internal readonly record struct MapCollisionHit(
	bool Hit, NumericsVector Position, NumericsVector Normal, float Fraction,
	ushort Material, PhysicsShape Shape, uint AcceptedTriangles );
