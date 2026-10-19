namespace SkateMotion.Core.Input;

/// <summary>A selected profile path and its measured quality.</summary>
internal readonly record struct GesturePatternMatch( int PatternIndex, float Quality, float Error, float Span );
