namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;

/// <summary>The retained frame, fresh duration and immediate physical relocation request for retrieval.</summary>
internal readonly record struct BoardRetrievalInitialization(
	LimbRigidTransform Source, float Duration, bool Relocate );
