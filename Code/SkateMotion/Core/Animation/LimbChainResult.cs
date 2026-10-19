namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Updated segment transforms and the displacement left after target adjustment.</summary>
internal readonly record struct LimbChainResult(
	bool Applied, LimbRigidTransform Upper, LimbRigidTransform Lower, Vector3 Residual );
