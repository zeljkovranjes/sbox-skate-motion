namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

internal static partial class PhysicalLocomotionCadence
{
	/// <summary>Consumes retained animation attributes alongside explicitly prepared physical inputs.</summary>
	internal static bool TryUpdateFromAttributes( ref PhysicalLocomotionCadenceState state,
		in PhysicalLocomotionCadenceInput physicalInput, Vector4 thresholds,
		PhysicalAnimationAttributeState attributes, PhysicalLocomotionCadenceContactInput? contacts )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		var translation = attributes.AnimationTranslation;
		var input = physicalInput with
		{
			AnimationMotion = new( translation.X, translation.Y, translation.Z ),
			RequestedDuration = attributes.AnimationTime,
			RequestedPhase = attributes.CadenceEndPercent
		};
		return TryUpdateCore( ref state, input, thresholds, contacts );
	}
}
