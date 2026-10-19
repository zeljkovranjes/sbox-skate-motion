namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Shared character, board and physical rig state across complete updates.</summary>
internal sealed class PhysicalCharacterState
{
	internal PhysicalRigState Rig { get; }
	internal PhysicalFrameOwner Output { get; }
	internal PhysicalFrameOwner BoardSource { get; }
	internal PhysicalBodyBinding BoardTarget { get; }
	internal PhysicalBodyBinding[] BoardBodies { get; }
	internal Vector4 BoardOrientation { get; set; }
	internal byte BoardCorrectionFlag { get; set; }
	internal int PhysicsMode { get; set; }
	internal PhysicalRigPlacement Placement { get; set; }
	internal LimbRigidTransform RootFrame { get; set; }
	internal LimbRigidTransform AuthoredFrame { get; set; }
	internal LimbRigidTransform RelativeBodyFrame { get; set; }

	internal PhysicalCharacterState( PhysicalRigState rig, PhysicalFrameOwner output,
		PhysicalFrameOwner boardSource, PhysicalBodyBinding boardTarget, PhysicalBodyBinding[] boardBodies )
	{
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( boardSource );
		ArgumentNullException.ThrowIfNull( boardTarget );
		ArgumentNullException.ThrowIfNull( boardBodies );
		if ( boardBodies.Length < 7 )
			throw new ArgumentException( "The board skeleton requires a seventh reference body." );
		Rig = rig;
		Output = output;
		BoardSource = boardSource;
		BoardTarget = boardTarget;
		BoardBodies = boardBodies;
	}
}
