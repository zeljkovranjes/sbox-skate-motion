namespace SkateMotion.Core.Animation;

/// <summary>Retains the local root used by the offboard character's world-frame publication.</summary>
internal sealed class PhysicalOffboardCharacterState
{
	internal LimbRigidTransform LocalRootFrame { get; set; }
}
