namespace SkateMotion.Core.Animation;

/// <summary>An authored clip attribute retaining its ordered lookup key, timing and value representation.</summary>
internal sealed class AnimationClipAttribute
{
	internal AnimationClipAttribute( AnimationAttributeKey name, float start, float end,
		sbyte kind, AnimationAttributePayload payload, AnimationScalarCurve curve = null )
	{
		if ( kind == 2 && curve is null )
			throw new ArgumentNullException( nameof( curve ) );
		Name = name;
		Start = start;
		End = end;
		Kind = kind;
		Payload = payload;
		Curve = curve;
	}

	internal AnimationAttributeKey Name { get; }
	internal float Start { get; }
	internal float End { get; }
	internal sbyte Kind { get; }
	internal AnimationAttributePayload Payload { get; }
	internal AnimationScalarCurve Curve { get; }
}
