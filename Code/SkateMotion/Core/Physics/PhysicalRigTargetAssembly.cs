using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Constructs four independent, nonresponding bodies for physical pose targets.</summary>
internal sealed class PhysicalRigTargetAssembly
{
	internal PhysicalBodyBinding[] Bindings { get; } = new PhysicalBodyBinding[ 4 ];
	internal PhysicalBodyMassResult Descriptor { get; }

	internal PhysicalRigTargetAssembly( float axisTolerance, Vector3 defaultAcceleration, int accumulatorState )
	{
		if ( !float.IsFinite( axisTolerance ) || axisTolerance <= 0f )
			throw new ArgumentOutOfRangeException( nameof( axisTolerance ) );
		if ( !float.IsFinite( defaultAcceleration.X ) || !float.IsFinite( defaultAcceleration.Y )
			|| !float.IsFinite( defaultAcceleration.Z ) )
			throw new ArgumentException( "Default target acceleration must be finite.", nameof( defaultAcceleration ) );
		var properties = PhysicalPrimitiveMassProperties.RoundedBox( new Vector3( .1f ), 0f );
		Descriptor = PhysicalBodyMassConstruction.Create( properties, LimbRigidTransform.Identity, 100f, 1f, axisTolerance );
		for ( var i = 0; i < Bindings.Length; ++i )
		{
			var body = new PhysicalBodyState { DefaultLinearAccumulator = defaultAcceleration };
			PhysicalBodyInitialization.Initialize( body, LimbRigidTransform.Identity, Descriptor.Mass, 1, accumulatorState );
			Bindings[ i ] = new() { Body = body, CachedPose = LimbRigidTransform.Identity, LocalOffset = Descriptor.LocalOffset };
		}
	}
}
