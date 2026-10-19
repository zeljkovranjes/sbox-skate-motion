using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Constructs distinct physical bodies and bindings for a canonical rider skeleton.</summary>
internal sealed class PhysicalRigBodyAssembly
{
	internal PhysicalBodyBinding[] Bindings { get; } = new PhysicalBodyBinding[ 26 ];
	internal PhysicalRigModeBody[] ModeBodies { get; } = new PhysicalRigModeBody[ 26 ];
	internal PhysicalRigBodyConstructionResult[] Primitives { get; } = new PhysicalRigBodyConstructionResult[ 24 ];
	internal PhysicalCompoundBodyGeometry? Compound { get; private set; }
	internal PhysicalFeedbackBodyConstructionResult[] Feedback { get; } = new PhysicalFeedbackBodyConstructionResult[ 2 ];
	internal float[] PoseVolumes { get; } = new float[ 24 ];

	private PhysicalRigBodyAssembly()
	{
	}

	internal static PhysicalRigBodyAssembly Create( PhysicalRigSettingsAsset settings,
		bool compound, LimbSolverMath math, Vector3 defaultAcceleration )
	{
		ArgumentNullException.ThrowIfNull( settings );
		ArgumentNullException.ThrowIfNull( math );
		if ( settings.BodyConstruction is not { } tuning || (compound && settings.CompoundBody is null) )
			throw new ArgumentException( "Rig body construction requires matching authored settings." );
		if ( !float.IsFinite( defaultAcceleration.X ) || !float.IsFinite( defaultAcceleration.Y )
			|| !float.IsFinite( defaultAcceleration.Z ) )
			throw new ArgumentException( "Default body acceleration must be finite." );
		var result = new PhysicalRigBodyAssembly();
		for ( var i = 0; i < 24; ++i )
		{
			var definition = settings.Bones[ i ].Body;
			PhysicalRigBodyMassVariants masses;
			if ( i == 1 && compound )
			{
				var geometry = PhysicalCompoundBodyGeometry.Create( settings.CompoundBody.Value, math );
				result.Compound = geometry;
				result.PoseVolumes[ i ] = geometry.PoseVolume;
				masses = PhysicalRigBodyConstruction.CreateCompound( geometry,
					definition.PrimaryMassCoefficient, definition.AlternateMassCoefficient, tuning );
			}
			else
			{
				var primitive = PhysicalRigBodyConstruction.Create( definition, tuning );
				result.Primitives[ i ] = primitive;
				result.PoseVolumes[ i ] = primitive.PoseVolume;
				masses = new( primitive.Primary, primitive.Alternate, primitive.Descriptor );
			}
			result.Add( i, masses, defaultAcceleration, i == 0 ? 0u : 1u, 0f, 0f );
		}
		for ( var i = 24; i < 26; ++i )
		{
			var feedback = PhysicalFeedbackBodyConstruction.Create( i, tuning.AxisTolerance );
			result.Feedback[ i - 24 ] = feedback;
			result.Add( i, new( feedback.Descriptor.Mass, feedback.Descriptor.Mass, feedback.Descriptor ),
				defaultAcceleration, 0u, feedback.LinearDamping, feedback.AngularDamping );
		}
		return result;
	}

	private void Add( int index, PhysicalRigBodyMassVariants masses, Vector3 defaultAcceleration,
		uint shapeFlags, float linearDamping, float angularDamping )
	{
		var descriptor = masses.Descriptor;
		var body = new PhysicalBodyState { DefaultLinearAccumulator = defaultAcceleration };
		var frame = descriptor.LocalOffset is { } offset ? offset.RigidInverse() : LimbRigidTransform.Identity;
		PhysicalBodyInitialization.Initialize( body, frame, descriptor.Mass, 4, 0 );
		Bindings[ index ] = new() { Body = body, CachedPose = LimbRigidTransform.Identity, LocalOffset = descriptor.LocalOffset };
		ModeBodies[ index ] = new( body, masses.Primary, masses.Alternate )
		{
			MaximumInertia = descriptor.MaximumInertia, ShapeFlags = shapeFlags,
			LinearDamping = linearDamping, AngularDamping = angularDamping
		};
	}
}
