namespace SkateMotion.Core.Animation;

/// <summary>Entry-time height selection and takeoff context.</summary>
internal readonly record struct TrickHeightSettings(
	AnimationAttributeKey SourceAttribute, float? FixedHeight,
	bool UseGestureSpeed, bool ClampToAttribute, bool FromManual, bool FromGrind );
