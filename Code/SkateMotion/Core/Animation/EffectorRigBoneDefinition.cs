namespace SkateMotion.Core.Animation;

/// <summary>A named clip bone and its unnormalized rig-space bind and body dimensions.</summary>
internal sealed class EffectorRigBoneDefinition
{
	/// <summary>Name in the clip skeleton.</summary>
	public string ClipBone { get; set; }
	/// <summary>Bind quaternion X/Y/Z/W in rig space; components are preserved.</summary>
	public float[] BindRotation { get; set; }
	/// <summary>Bind translation in rig-space metres.</summary>
	public float[] BindPosition { get; set; }
	/// <summary>Physical dimensions in rig-space metres.</summary>
	public float[] BodySize { get; set; }
}
