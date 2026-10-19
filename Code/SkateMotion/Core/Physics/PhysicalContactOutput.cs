using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Exports solved rows into the world contact stream consumed by rider feedback.</summary>
internal static class PhysicalContactOutput
{
	internal static bool TryExport( ContactConstraint contact, PhysicalBodyState first,
		PhysicalBodyState second, float inverseStep, out PhysicalWorldContact output )
	{
		output = default;
		if ( !contact.ReportContact || !(contact.Impulse.X > 0f) )
			return false;
		var firstPoint = first.Pose.Translation + contact.OffsetA;
		var secondPoint = second.Pose.Translation + contact.OffsetB;
		var sum = contact.InverseMassA + contact.InverseMassB;
		var reciprocal = 1f / sum;
		var residual = MathF.FusedMultiplyAdd( -reciprocal, sum, 1f );
		reciprocal = MathF.FusedMultiplyAdd( residual, reciprocal, reciprocal );
		var point = MultiplyAdd( firstPoint, contact.InverseMassA,
			secondPoint * contact.InverseMassB ) * reciprocal;
		var scale = inverseStep * inverseStep;
		var normalImpulse = (contact.Normal * contact.Impulse.X) * scale;
		var tangentImpulse = MultiplyAdd( contact.TangentA, contact.Impulse.Y,
			contact.TangentB * contact.Impulse.Z ) * scale;
		output = new( first, second, new( contact.Normal, 0f ), new( point, 0f ), tangentImpulse, contact.Materials )
		{
			TangentA = contact.TangentA,
			TangentB = contact.TangentB,
			NormalImpulse = normalImpulse,
			TangentImpulse = new( tangentImpulse,
				MathF.FusedMultiplyAdd( 0f, contact.Impulse.Y, 0f * contact.Impulse.Z ) * scale )
		};
		return true;
	}

	private static Vector3 MultiplyAdd( Vector3 value, float scale, Vector3 addend ) => new(
		MathF.FusedMultiplyAdd( value.X, scale, addend.X ),
		MathF.FusedMultiplyAdd( value.Y, scale, addend.Y ),
		MathF.FusedMultiplyAdd( value.Z, scale, addend.Z ) );
}
