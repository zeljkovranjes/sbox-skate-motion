namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Bounds a hand pin around its raw animated anchor and publishes its request.</summary>
internal static class PhysicalHandPinController
{
	internal static void Update( EffectorRigState state, bool[] pinning, bool rightHand,
		LimbRigidTransform modelToWorld, Vector3 requested, float reach, float weight )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( pinning );
		if ( pinning.Length != 4 || state.Skeleton.AnimatedBones.Length < 10 )
			throw new ArgumentException( "Hand pins require four request slots and canonical arm bones." );

		var slot = rightHand ? 3 : 2;
		var bone = rightHand ? 9 : 5;
		var anchor = modelToWorld.Transform( state.Skeleton.AnimatedBones[ bone ].Translation );
		var offset = requested - anchor;
		var distance = SquareRoot( Dot( offset, offset ) );
		// Preserve the ordered comparison, including its non-finite branch behavior.
		var target = reach >= distance ? requested : AddScaled( offset, reach / distance, anchor );
		// Arithmetic-generated NaNs use the same positive quiet representation on each host.
		if ( !(reach >= distance) )
			target = new Vector3( CanonicalizeNaN( target.X ), CanonicalizeNaN( target.Y ), CanonicalizeNaN( target.Z ) );
		var pose = state.Poses[ slot ];
		state.Poses[ slot ] = pose with { TargetState = pose.TargetState with { Pin = target, Weight = weight } };
		pinning[ slot ] = true;
	}

	private static float CanonicalizeNaN( float value ) =>
		float.IsNaN( value ) ? BitConverter.UInt32BitsToSingle( 0x7fc00000 ) : value;
}
