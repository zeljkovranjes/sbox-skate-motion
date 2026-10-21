namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A named point a model was authored with, in its own frame (metres): a bone at its bind pose, with its index, or an
/// attachment (<see cref="Bone"/> -1).
/// </summary>
internal readonly record struct BoardHint( string Name, Vector3 Position, int Bone );
