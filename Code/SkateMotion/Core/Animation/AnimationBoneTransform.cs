namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;

/// <summary>A local bone transform retaining all four lanes of translation and scale.</summary>
internal readonly record struct AnimationBoneTransform( Vector4 Position, Quaternion Rotation, Vector4 Scale );
