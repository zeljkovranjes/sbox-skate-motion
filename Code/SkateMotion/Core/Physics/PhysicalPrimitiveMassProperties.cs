namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Calculates primitive mass properties with retained extent floors and rounding order.</summary>
internal static class PhysicalPrimitiveMassProperties
{
	private const float MinimumRadius = 1.0000000116860974e-7f;

	internal static PhysicalShapeMassProperties Sphere( float radius )
	{
		radius = MathF.Max( radius, MinimumRadius );
		var squared = radius * radius;
		return new( new Vector3( squared * .4f ), (radius * squared) * 4.188790321350098f );
	}

	internal static PhysicalShapeMassProperties Capsule( float radius, float halfLength )
	{
		radius = MathF.Max( MathF.Max( radius, MinimumRadius ), halfLength * .05f );
		var squared = radius * radius;
		var denominator = MultiplyAdd( radius, 4f, halfLength * 3f );
		var length = MultiplyAdd( radius, 1.3333333730697632f, halfLength + halfLength );
		var radial = squared * .4f;
		var axial = MultiplyAdd( radius * squared, 1.6f, halfLength * (squared * .75f) );
		axial = MultiplyAdd( halfLength, halfLength * halfLength, axial );
		axial = MultiplyAdd( halfLength * radius, halfLength * 4f, axial ) / denominator;
		return new( new Vector3( axial, axial, radial ), length * (squared * 3.1415927410125732f) );
	}

	internal static PhysicalShapeMassProperties Cylinder( float radius, float halfLength, float margin )
	{
		radius += margin;
		halfLength += margin;
		var squared = radius * radius;
		var axial = MultiplyAdd( squared, 3f, (halfLength * halfLength) * 4f ) * .0833333358168602f;
		return new( new Vector3( axial, axial, squared * .5f ), halfLength * (squared * 6.2831854820251465f) );
	}

	internal static PhysicalShapeMassProperties RoundedBox( Vector3 halfExtents, float margin )
	{
		var largest = MathF.Max( halfExtents.X, MathF.Max( halfExtents.Y, halfExtents.Z ) );
		var minimum = MathF.Max( (largest + margin) * .05f, MinimumRadius ) - margin;
		var x = MathF.Max( halfExtents.X, minimum );
		var y = MathF.Max( halfExtents.Y, minimum );
		var z = MathF.Max( halfExtents.Z, minimum );
		var xx = x * x;
		var yy = y * y;
		var zz = z * z;
		var xy = x * y;
		var twiceMargin = margin + margin;
		var marginSquared = margin * margin;
		var radial = marginSquared * .5f;
		var inertia = new Vector3(
			MultiplyAdd( MultiplyAdd( y + z, twiceMargin, yy + zz ), .3333333432674408f, radial ),
			MultiplyAdd( MultiplyAdd( twiceMargin, x + z, zz + xx ), .3333333432674408f, radial ),
			MultiplyAdd( MultiplyAdd( x + y, twiceMargin, yy + xx ), .3333333432674408f, radial ) );
		var surface = MultiplyAdd( y, z, MultiplyAdd( x, z, xy ) );
		var edges = ((x + y) + MultiplyAdd( margin, .6666666865348816f, z ))
			* (marginSquared * 6.2831854820251465f);
		var volume = MultiplyAdd( MultiplyAdd( z, xy, margin * surface ), 8f, edges );
		return new( inertia, volume );
	}
}
