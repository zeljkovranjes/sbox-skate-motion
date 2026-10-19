using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

/// <summary>Applies the authored hand-helper correction before adapting the board to a display rig.</summary>
internal sealed class CarriedBoardAttachment
{
	private readonly EffectorRigAsset _rig;
	private readonly int _helper;
	private readonly LimbRigidTransform _bind;
	private readonly BoardAttachmentController _controller = new();
	private readonly LimbRigidTransform[] _authored = new LimbRigidTransform[ 4 ];

	internal CarriedBoardAttachment( EffectorRigAsset rig, bool leftHand )
	{
		_rig = rig;
		_helper = rig.HelperClipBones[ leftHand ? 2 : 3 ];
		_bind = Frame( rig.BindPositions[ 0 ], rig.BindRotations[ 0 ] );
	}

	internal LimbRigidTransform Update( PoseRetargeter pose, int board, int hand )
	{
		var handFrame = _rig.Space.ToRig( Frame( pose.SourcePosition( hand ), pose.SourceRotation( hand ) ) );
		var helper = _rig.Space.ToRig( Frame( pose.SourcePosition( _helper ), pose.SourceRotation( _helper ) ) );
		var result = _rig.Space.ToRig( Frame( pose.SourcePosition( board ), pose.SourceRotation( board ) ) ).Compose( _bind );
		_controller.RequestFromHand( handFrame, helper );
		_controller.Update( ref result, _authored );
		return _rig.Space.ToClip( result );
	}

	private static LimbRigidTransform Frame( System.Numerics.Vector3 position, System.Numerics.Quaternion rotation ) => new(
		new LimbFrame( System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitX, rotation ),
			System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitY, rotation ),
			System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitZ, rotation ) ), position );
}
