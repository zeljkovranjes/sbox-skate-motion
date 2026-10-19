namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Converts between rig and clip axes without normalizing or rescaling transforms.</summary>
internal sealed class RigCoordinateSpace
{
	private readonly int[] _clipAxes;
	private readonly int[] _rigAxes = new int[ 3 ];

	internal RigCoordinateSpace( int[] clipAxes )
	{
		if ( clipAxes is null || clipAxes.Length != 3 )
			throw new ArgumentException( "Three signed, one-based clip axes are required." );
		_clipAxes = new int[ 3 ];
		for ( var i = 0; i < 3; i++ )
		{
			var axis = clipAxes[ i ];
			if ( axis == 0 || axis < -3 || axis > 3 || _rigAxes[ Math.Abs( axis ) - 1 ] != 0 )
				throw new ArgumentException( "Clip axes must be a signed permutation of 1, 2, 3." );
			_clipAxes[ i ] = axis;
			_rigAxes[ Math.Abs( axis ) - 1 ] = Math.Sign( axis ) * (i + 1);
		}
		var x = ToClip( Vector3.UnitX );
		var y = ToClip( Vector3.UnitY );
		if ( Vector3.Dot( Vector3.Cross( x, y ), ToClip( Vector3.UnitZ ) ) != 1f )
			throw new ArgumentException( "Rig and clip axes must have the same handedness." );
	}

	internal Vector3 ToClip( Vector3 value ) => Map( value, _clipAxes );
	internal Vector3 ToRig( Vector3 value ) => Map( value, _rigAxes );
	internal Quaternion ToClip( Quaternion value ) => Map( value, _clipAxes );
	internal Quaternion ToRig( Quaternion value ) => Map( value, _rigAxes );
	internal LimbRigidTransform ToClip( LimbRigidTransform value ) => Map( value, _clipAxes );
	internal LimbRigidTransform ToRig( LimbRigidTransform value ) => Map( value, _rigAxes );

	private static Vector3 Map( Vector3 value, int[] axes ) => new(
		Component( value, axes[ 0 ] ), Component( value, axes[ 1 ] ), Component( value, axes[ 2 ] ) );

	private static Quaternion Map( Quaternion value, int[] axes )
	{
		var vector = Map( new Vector3( value.X, value.Y, value.Z ), axes );
		return new Quaternion( vector, value.W );
	}

	private static LimbRigidTransform Map( LimbRigidTransform value, int[] axes ) => new(
		new LimbFrame( Map( Column( value.Rotation, axes[ 0 ] ), axes ),
			Map( Column( value.Rotation, axes[ 1 ] ), axes ),
			Map( Column( value.Rotation, axes[ 2 ] ), axes ) ), Map( value.Translation, axes ) );

	private static float Component( Vector3 value, int axis )
	{
		var component = Math.Abs( axis ) switch { 1 => value.X, 2 => value.Y, _ => value.Z };
		return axis < 0 ? -component : component;
	}

	private static Vector3 Column( LimbFrame frame, int axis )
	{
		var column = Math.Abs( axis ) switch { 1 => frame.X, 2 => frame.Y, _ => frame.Z };
		return axis < 0 ? -column : column;
	}
}
