using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Combined shape properties and principal mass frame for the authored compound body.</summary>
internal readonly record struct PhysicalCompoundMassProperties( PhysicalShapeMassProperties Properties, LimbRigidTransform Frame )
{
	internal static PhysicalMassMoments CreateMoments( PhysicalCompoundBodyGeometry geometry )
	{
		var cylinder = PhysicalPrimitiveMassProperties.Cylinder( geometry.CylinderRadius, geometry.CylinderHalfLength, 0f );
		var sphere = PhysicalPrimitiveMassProperties.Sphere( geometry.SphereRadius );
		var first = PhysicalMassMoments.FromPrimitive( cylinder ).Transform( geometry.CylinderFrame );
		var second = PhysicalMassMoments.FromPrimitive( sphere ).Transform( LimbRigidTransform.Identity );
		return first.Add( second ).Transform( LimbRigidTransform.Identity );
	}

	internal static PhysicalCompoundMassProperties Create( PhysicalCompoundBodyGeometry geometry )
	{
		var moments = CreateMoments( geometry );
		var inverseVolume = Reciprocal( moments.W.W );
		var center = new Vector3( moments.W.X, moments.W.Y, moments.W.Z ) * inverseVolume;
		var centered = moments.Transform( new( LimbRigidTransform.Identity.Rotation, -center ) );
		Span<float> matrix = stackalloc float[ 9 ];
		Span<float> axes = stackalloc float[ 9 ];
		centered.WriteInertia( matrix );
		PhysicalInertiaDiagonalization.Solve( matrix, axes, 9.999999960041972e-13f, 10 );
		var floor = MathF.Max( MathF.Max( matrix[ 0 ], matrix[ 4 ] ), matrix[ 8 ] ) * .005000000353902578f;
		var inertia = new Vector3( MathF.Max( matrix[ 0 ], floor ), MathF.Max( matrix[ 4 ], floor ), MathF.Max( matrix[ 8 ], floor ) ) * inverseVolume;
		var frame = new LimbFrame( new( axes[ 0 ], axes[ 3 ], axes[ 6 ] ),
			new( axes[ 1 ], axes[ 4 ], axes[ 7 ] ), new( axes[ 2 ], axes[ 5 ], axes[ 8 ] ) );
		return new( new( inertia, moments.W.W ), new( frame, center ) );
	}
}
