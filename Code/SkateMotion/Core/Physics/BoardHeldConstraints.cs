using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Quaternion = System.Numerics.Quaternion;

/// <summary>Retains each hand's attachment frame and drive through activation and release.</summary>
internal sealed class BoardHeldConstraints
{
	private readonly PhysicalJointFrame[] _frames = new PhysicalJointFrame[ 2 ];
	private readonly DriveSettings[] _drives = { Disabled, Disabled };
	private static DriveSettings Disabled => new( DriveMode.SpeedLimited, 0f, 0f, 0f );
	private static DriveSettings Active => new( DriveMode.SpeedLimited, 299.9999694824219f, 0f, 17999.998046875f );

	internal int SelectedHand { get; private set; } = 2;
	internal PhysicalJointFrame Frame( int hand ) => _frames[ hand ];
	internal DriveSettings Drive( int hand ) => _drives[ hand ];

	internal DriveGeometry Geometry( int hand, int handBody, int boardBody )
	{
		var frame = _frames[ hand ];
		var child = frame.ChildRotation;
		var body = frame.BodyRotation;
		return new( handBody, boardBody, frame.ChildPosition, frame.BodyPosition,
			new Quaternion( child.X, child.Y, child.Z, child.W ),
			new Quaternion( body.X, body.Y, body.Z, body.W ) );
	}

	internal void Activate( int hand, LimbRigidTransform board, LimbRigidTransform attachment )
	{
		if ( (uint)hand >= 2 )
			throw new ArgumentOutOfRangeException( nameof( hand ) );
		SelectedHand = hand;
		_drives[ hand ] = Active;
		Update( board, attachment );
	}

	internal void Update( LimbRigidTransform board, LimbRigidTransform attachment )
	{
		if ( SelectedHand != 2 )
			_frames[ SelectedHand ] = BoardHeldFrame.Create( board, attachment );
	}

	internal void Release()
	{
		if ( SelectedHand == 2 )
			return;
		_drives[ SelectedHand ] = Disabled;
		SelectedHand = 2;
	}
}
