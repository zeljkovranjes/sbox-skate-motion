namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using A = PhysicalCadenceArithmetic;
using O = PhysicalLocomotionOwnerArithmetic;
using C = SkateMotion.Core.Collision.CollisionArithmetic;
using M = LimbSolverMath;

/// <summary>Retains the ground-query frame assembled from completed physical state and pre-stance solved axes.</summary>
internal sealed class PhysicalGroundQueryContext
{
	private readonly uint[] _frame = new uint[ 28 ];
	internal ReadOnlySpan<uint> Frame => _frame;
	internal int InstanceFilter { get; }
	internal bool AlternateWorld { get; }

	internal PhysicalGroundQueryContext( int instanceFilter, bool alternateWorld )
	{
		InstanceFilter = instanceFilter;
		AlternateWorld = alternateWorld;
	}

	/// <summary>Copies entity query policy to its retained owner; the adapter must select the matching world.</summary>
	internal void BindOwner( Span<uint> owner )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount )
			throw new ArgumentException( "A complete retained query owner is required.", nameof( owner ) );
		owner[ 0x138 / 4 ] = AlternateWorld ? 1u : 0u;
		owner[ 0x6298 / 4 ] = unchecked((uint)InstanceFilter);
	}

	/// <summary>Runs after physical frame adoption and before issuing queries, retaining prior contact feedback.</summary>
	internal void Capture( PhysicalLocomotionOwnerState owner, Vector3 orientationAxis,
		PhysicalLocomotionFrameState adopted, ReadOnlySpan<uint> retainedQuery )
	{
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( adopted );
		if ( retainedQuery.Length < PhysicalGroundQueryPublication.RecordWordCount )
			throw new ArgumentException( "The complete retained contact publication is required.", nameof( retainedQuery ) );
		var secondary = owner.SecondaryFrame;
		var position = secondary.Translation;
		if ( (retainedQuery[ 0xb0 / 4 ] & 1u) != 0 )
		{
			var up = new Vector3( secondary.M21, secondary.M22, secondary.M23 );
			var point = new Vector3( Value( retainedQuery[ 0 ] ), Value( retainedQuery[ 1 ] ), Value( retainedQuery[ 2 ] ) );
			var distance = M.Dot( point - position, up );
			if ( distance > 0f )
				position = M.AddScaled( up, distance, position );
		}
		var forward = new Vector3( owner.Frame.M31, owner.Frame.M32, owner.Frame.M33 );
		var basis = BuildBasis( forward, orientationAxis );
		Write( 0, position );
		Write( 4, basis.Z );
		Write( 8, basis.Y );
		Write( 12, basis.X );
		Write( 16, owner.Solved.Motion );
		Write( 20, adopted.QueryAxes.Y );
		Write( 24, adopted.QueryAxes.X );
	}

	internal static LimbFrame BuildBasis( Vector3 forward, Vector3 orientationAxis )
	{
		var side = O.Cross( orientationAxis, forward );
		var length = A.Length( side );
		if ( !A.Greater( length, A.Value( 0x37800000 ) ) )
			return new( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ );
		var inverse = C.Value( C.Interpolate( C.Word( length ), C.ReciprocalEstimate( C.Word( length ) ) ) );
		inverse = A.MultiplyAdd( inverse, A.NegativeMultiplySubtract( inverse, length, 1f ), inverse );
		side = O.Scale( side, inverse );
		var up = O.Normalize( orientationAxis );
		return new( side, up, O.Normalize( O.Cross( side, up ) ) );
	}

	private static float Value( uint word ) => BitConverter.UInt32BitsToSingle( word );

	private void Write( int offset, Vector3 value )
	{
		_frame[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		_frame[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		_frame[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		// The existing physical and mesh adapters use three-component query vectors.
		_frame[ offset + 3 ] = 0;
	}
}
