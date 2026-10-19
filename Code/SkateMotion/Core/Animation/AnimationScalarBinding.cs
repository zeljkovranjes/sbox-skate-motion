namespace SkateMotion.Core.Animation;

/// <summary>Maps one supplied scalar and its coordinate mode to an authored attribute key.</summary>
internal sealed class AnimationScalarBinding
{
	public string Parameter { get; set; }
	public string Attribute { get; set; }
	public bool Normalized { get; set; }
}
