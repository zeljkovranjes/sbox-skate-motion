using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Builds canonical primitive and compound bodies with their retained mass variants.</summary>
internal static class PhysicalRigBodyConstruction
{
	internal static PhysicalRigBodyConstructionResult Create(
		PhysicalRigBodyDefinition definition, PhysicalRigBodyTuning tuning )
	{
		var dimensions = definition.Dimensions;
		var volume = (dimensions.X * dimensions.Y) * dimensions.Z;
		var massBasis = volume * tuning.MassScale;
		var extents = Vector3.Zero;
		var radius = 0f;
		var halfLength = 0f;
		int shape;
		PhysicalShapeMassProperties properties;
		if ( definition.IsRoot )
		{
			shape = 2;
			radius = tuning.RootRadius;
			halfLength = tuning.RootHalfLength;
			volume = 0f;
			massBasis = .001f;
			properties = PhysicalPrimitiveMassProperties.Capsule( radius, halfLength );
		}
		else if ( definition.Shape == 0 )
		{
			shape = 1;
			extents = dimensions * (definition.SizeScale * .5f);
			properties = PhysicalPrimitiveMassProperties.RoundedBox( extents, 0f );
		}
		else if ( definition.Shape == 1 )
		{
			shape = 3;
			radius = MathF.Max( dimensions.X * .5f, dimensions.Y * .5f ) * definition.SizeScale;
			properties = PhysicalPrimitiveMassProperties.Sphere( radius );
		}
		else if ( definition.Shape == 2 )
		{
			shape = 2;
			var unscaledRadius = tuning.CapsuleRadiusScale * (dimensions.X * .5f);
			radius = unscaledRadius * definition.SizeScale;
			halfLength = MathF.Max( dimensions.Z * .5f - unscaledRadius, 0f )
				* (definition.SizeScale * tuning.CapsuleLengthScale);
			properties = PhysicalPrimitiveMassProperties.Capsule( radius, halfLength );
		}
		else
		{
			throw new ArgumentOutOfRangeException( nameof( definition ), "Unsupported canonical primitive shape." );
		}

		var masses = CreateMasses( properties, LimbRigidTransform.Identity, massBasis,
			definition.PrimaryMassCoefficient, definition.AlternateMassCoefficient, tuning );
		return new( shape, extents, radius, halfLength, volume, masses.Primary, masses.Alternate, masses.Descriptor );
	}

	internal static PhysicalRigBodyMassVariants CreateCompound( PhysicalCompoundBodyGeometry geometry,
		float primaryMassCoefficient, float alternateMassCoefficient, PhysicalRigBodyTuning tuning )
	{
		var properties = PhysicalCompoundMassProperties.Create( geometry );
		var masses = CreateMasses( properties.Properties, properties.Frame, geometry.MassBasis,
			primaryMassCoefficient, alternateMassCoefficient, tuning );
		// This authored body retains an explicit identity offset after mass construction.
		return masses with { Descriptor = masses.Descriptor with { LocalOffset = LimbRigidTransform.Identity } };
	}

	private static PhysicalRigBodyMassVariants CreateMasses( PhysicalShapeMassProperties properties,
		LimbRigidTransform frame, float massBasis, float primaryMassCoefficient,
		float alternateMassCoefficient, PhysicalRigBodyTuning tuning )
	{
		var alternateMass = alternateMassCoefficient * massBasis;
		var alternate = PhysicalBodyMassConstruction.Create( properties, frame,
			alternateMass, 1f, tuning.AxisTolerance );
		var primaryMass = primaryMassCoefficient * massBasis;
		var descriptor = PhysicalBodyMassConstruction.Create( properties, frame,
			primaryMass, 1f, tuning.AxisTolerance );
		descriptor = ModifyInertia( descriptor, tuning.InertiaMode, tuning.InertiaScale );
		return new(
			new( descriptor.Mass.InverseInertia, 1f / primaryMass ),
			new( alternate.Mass.InverseInertia, tuning.AlternateMassScale / alternateMass ), descriptor );
	}

	private static PhysicalBodyMassResult ModifyInertia( PhysicalBodyMassResult descriptor, int mode, float scale )
	{
		var inertia = descriptor.Mass.InverseInertia;
		if ( mode == 1 )
		{
			inertia *= Reciprocal( scale );
		}
		else if ( mode == 2 )
		{
			var inverseScale = 1f / (scale * 3f);
			inertia = new Vector3( inverseScale * ((inertia.X + inertia.Y) + inertia.Z) );
		}
		else
		{
			return descriptor;
		}
		return descriptor with
		{
			Mass = descriptor.Mass with { InverseInertia = inertia },
			MaximumInertia = 1f / MathF.Min( MathF.Min( inertia.X, inertia.Y ), inertia.Z )
		};
	}
}
