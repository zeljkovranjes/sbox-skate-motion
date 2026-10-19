namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Retained bone and group feedback shared by rig modes and pose input.</summary>
internal sealed class PhysicalRigFeedbackState
{
	internal float[] BoneWeights { get; } = new float[ 24 ];
	internal float[] AuthoredWeights { get; } = new float[ 24 ];
	internal byte[] BoneFlags { get; } = new byte[ 24 ];
	internal byte[] AuthoredFlags { get; } = new byte[ 24 ];
	// Preserve the typed payload lanes until their individual producers assign meaning.
	internal Vector4[] BoneVectors { get; } = new Vector4[ 24 * 5 ];
	internal float[] BoneScalars { get; } = new float[ 24 * 2 ];
	internal int[] BoneValues { get; } = new int[ 24 * 2 ];
	internal byte[] BoneControlFlags { get; } = new byte[ 24 * 4 ];
	internal float[] GroupScalars { get; } = new float[ 8 * 3 ];
	internal Vector4[] GroupVectors { get; } = new Vector4[ 8 ];
	internal int[] GroupValues { get; } = new int[ 8 ];
	internal int[] GroupIndices { get; } = new int[ 8 ];
	internal float[] ControlScalars { get; } = new float[ 10 ];
	internal byte[] ControlFlags { get; } = new byte[ 14 ];
	internal Vector4 PrimaryReference { get; set; }
	internal Vector4 ReferencePosition { get; set; }
	internal Vector3 FirstDelta { get; set; }
	internal Vector3 SecondDelta { get; set; }
	internal int SelectedIndex { get; set; }
	internal int Counter { get; set; }
	internal float Blend
	{
		get => ControlScalars[ 1 ];
		set => ControlScalars[ 1 ] = value;
	}

	internal void Reset()
	{
		Array.Clear( BoneWeights );
		Array.Clear( AuthoredWeights );
		Array.Clear( BoneFlags );
		Array.Clear( BoneVectors );
		Array.Clear( BoneScalars );
		Array.Clear( BoneValues );
		Array.Clear( BoneControlFlags );
		Array.Clear( GroupScalars );
		Array.Clear( GroupVectors );
		Array.Clear( GroupValues );
		Array.Fill( GroupIndices, -1 );
		Array.Clear( ControlScalars );
		Array.Clear( ControlFlags );
		PrimaryReference = Vector4.UnitY;
		ReferencePosition = Vector4.UnitY;
		FirstDelta = Vector3.Zero;
		SecondDelta = Vector3.Zero;
		SelectedIndex = -1;
		Counter = 0;
	}

	internal void EnterPrimary( ReadOnlySpan<byte> flags, ReadOnlySpan<float> weights )
	{
		if ( flags.Length != 24 || weights.Length != 24 )
			throw new ArgumentException( "Primary mode requires all 24 authored flags and weights." );
		Reset();
		for ( var i = 1; i < 24; ++i )
		{
			AuthoredFlags[ i ] = flags[ i ];
			AuthoredWeights[ i ] = weights[ i ];
		}
		AuthoredFlags[ 0 ] = 1;
	}

	internal PhysicalRigInput BindInput( PhysicalRigInput input ) => input with
	{
		JointSettings = input.JointSettings with { Blend = Blend },
		Frames = input.Frames with
		{
			ReferencePosition = new Vector3( ReferencePosition.X, ReferencePosition.Y, ReferencePosition.Z )
		}
	};
}
