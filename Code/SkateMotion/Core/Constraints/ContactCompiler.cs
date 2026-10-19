namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

internal static class ContactCompiler
{
	internal static ContactConstraint Compile( in DynamicBody a, in DynamicBody b,
		in CollisionContact contact, float step )
	{
		var inputs = ContactInputs.FromBodies( a, b, contact );
		return Compile( inputs.A, inputs.B, inputs.Geometry, step );
	}

	// motionScale is timestep; MotionTerm is relative point velocity. Body motion fields are accelerations.
	internal static ContactConstraint Compile( in ContactBody a, in ContactBody b,
		in ContactGeometry geometry, float motionScale )
	{
		if ( !float.IsFinite( motionScale ) || motionScale < 0f )
			throw new ArgumentOutOfRangeException( nameof( motionScale ) );

		var offsetA = geometry.PointA - a.Center;
		var offsetB = geometry.PointB - b.Center;
		var normal = CompileDirection( a, b, offsetA, offsetB, geometry.Normal );
		var tangentA = CompileDirection( a, b, offsetA, offsetB, geometry.TangentA );
		var tangentB = CompileDirection( a, b, offsetA, offsetB, geometry.TangentB );
		var separation = geometry.PointB - geometry.PointA;
		var relativeMotion = b.PointMotion( offsetB ) - a.PointMotion( offsetA );
		var scaledMotion = geometry.MotionTerm * motionScale;
		var predicted = separation + relativeMotion * (motionScale * motionScale) + scaledMotion;
		var bounce = -scaledMotion * geometry.Restitution;
		var normalSeparation = Vector3.Dot( separation, normal.Coefficient );
		var normalBounce = Vector3.Dot( bounce, normal.Coefficient );
		var normalBias = Vector3.Dot( predicted, normal.Coefficient );
		if ( normalBias > 0f )
		{
			var adjustment = normalSeparation >= 0f ? normalBounce
				: normalBounce < 0f ? normalSeparation + normalBounce : 0f;
			normalBias -= adjustment;
		}

		return new ContactConstraint
		{
			BodyA = geometry.BodyA,
			BodyB = geometry.BodyB,
			ReportContact = ((a.MotionFlags | b.MotionFlags) & 8) != 0,
			Materials = geometry.Materials,
			InverseMassA = a.EffectiveInverseMass,
			InverseMassB = b.EffectiveInverseMass,
			OffsetA = offsetA,
			OffsetB = offsetB,
			Normal = geometry.Normal,
			TangentA = geometry.TangentA,
			TangentB = geometry.TangentB,
			CoefficientX = new Vector3( normal.Coefficient.X, tangentA.Coefficient.X, tangentB.Coefficient.X ),
			CoefficientY = new Vector3( normal.Coefficient.Y, tangentA.Coefficient.Y, tangentB.Coefficient.Y ),
			CoefficientZ = new Vector3( normal.Coefficient.Z, tangentA.Coefficient.Z, tangentB.Coefficient.Z ),
			NormalAngularA = normal.AngularA,
			NormalAngularB = normal.AngularB,
			TangentAAngularA = tangentA.AngularA,
			TangentAAngularB = tangentA.AngularB,
			TangentBAngularA = tangentB.AngularA,
			TangentBAngularB = tangentB.AngularB,
			StaticFriction = geometry.StaticFriction,
			SlidingFriction = geometry.SlidingFriction,
			Bias = new Vector4( normalBias, Vector3.Dot( predicted, tangentA.Coefficient ),
				Vector3.Dot( predicted, tangentB.Coefficient ), normalSeparation ),
			Impulse = Vector4.Zero
		};
	}

	private static (Vector3 Coefficient, Vector3 AngularA, Vector3 AngularB) CompileDirection(
		in ContactBody a, in ContactBody b, Vector3 offsetA, Vector3 offsetB, Vector3 direction )
	{
		var torqueA = Vector3.Cross( offsetA, direction );
		var torqueB = Vector3.Cross( offsetB, direction );
		var angularA = a.AngularResponse( torqueA );
		var angularB = b.AngularResponse( torqueB );
		var denominator = a.EffectiveInverseMass + b.EffectiveInverseMass
			+ Vector3.Dot( torqueA, angularA ) + Vector3.Dot( torqueB, angularB );
		if ( !float.IsFinite( denominator ) || denominator <= 0f )
			throw new ArgumentException( "A contact direction must have positive finite effective inverse mass." );

		return (direction / denominator, angularA, angularB);
	}
}
