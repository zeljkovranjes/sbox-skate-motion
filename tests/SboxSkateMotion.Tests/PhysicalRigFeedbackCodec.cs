using System.Buffers.Binary;
using System.Numerics;
using System.Reflection;
using SkateMotion.Core.Animation;

namespace SboxSkateMotion.Tests;

internal static class PhysicalRigFeedbackCodec
{
	internal static PhysicalRigFeedbackState Read( byte[] raw )
	{
		var state = new PhysicalRigFeedbackState();
		Visit( state, raw, false );
		return state;
	}

	internal static byte[] Write( PhysicalRigFeedbackState state, byte[] original )
	{
		var raw = original.ToArray();
		Visit( state, raw, true );
		return raw;
	}

	internal static bool IsPadding( int index ) => index is >= 0x49c and < 0x4a0 or >= 0x4ac and < 0x4b0;

	private static void Visit( PhysicalRigFeedbackState state, byte[] raw, bool write )
	{
		void Field( string name, int index, int offset )
		{
			var property = typeof( PhysicalRigFeedbackState ).GetProperty( name, BindingFlags.Instance | BindingFlags.NonPublic )!;
			var array = property.GetValue( state ) as Array;
			var type = array?.GetType().GetElementType() ?? property.PropertyType;
			if ( write )
				Store( raw, offset, array is null ? property.GetValue( state )! : array.GetValue( index )! );
			else if ( array is null )
				property.SetValue( state, Load( raw, offset, type! ) );
			else
				array.SetValue( Load( raw, offset, type! ), index );
		}
		for ( var i = 0; i < 24; ++i )
		{
			Field( "BoneWeights", i, 0x280 + i * 4 );
			Field( "AuthoredWeights", i, 0x2e0 + i * 4 );
			Field( "BoneFlags", i, 0x340 + i );
			Field( "AuthoredFlags", i, 0x358 + i );
			var record = 0x4d0 + i * 0x70;
			for ( var j = 0; j < 5; ++j )
				Field( "BoneVectors", i * 5 + j, record + j * 16 );
			for ( var j = 0; j < 2; ++j )
			{
				Field( "BoneScalars", i * 2 + j, record + 0x50 + j * 4 );
				Field( "BoneValues", i * 2 + j, record + 0x58 + j * 4 );
			}
			for ( var j = 0; j < 4; ++j )
				Field( "BoneControlFlags", i * 4 + j, record + 0x60 + j );
		}
		for ( var i = 0; i < 8; ++i )
		{
			for ( var j = 0; j < 3; ++j )
				Field( "GroupScalars", i * 3 + j, 0x370 + i * 4 + j * 0x20 );
			Field( "GroupValues", i, 0x3d0 + i * 4 );
			Field( "GroupVectors", i, 0x3f0 + i * 16 );
			Field( "GroupIndices", i, 0x4b0 + i * 4 );
		}
		Field( "PrimaryReference", 0, 0x470 );
		Field( "ReferencePosition", 0, 0x480 );
		Field( "FirstDelta", 0, 0x490 );
		Field( "SecondDelta", 0, 0x4a0 );
		Field( "SelectedIndex", 0, 0xfd4 );
		Field( "Counter", 0, 0xff4 );
		for ( var i = 0; i < 10; ++i )
			Field( "ControlScalars", i, 0xfb8 + i * 4 + (i >= 7 ? 4 : 0) );
		for ( var i = 0; i < 14; ++i )
			Field( "ControlFlags", i, 0xfe4 + i );
	}

	private static float Float( byte[] raw, int at ) => BitConverter.Int32BitsToSingle( BinaryPrimitives.ReadInt32BigEndian( raw.AsSpan( at, 4 ) ) );

	private static object Load( byte[] raw, int at, Type type )
	{
		if ( type == typeof( byte ) )
			return raw[ at ];
		if ( type == typeof( int ) )
			return BinaryPrimitives.ReadInt32BigEndian( raw.AsSpan( at, 4 ) );
		if ( type == typeof( float ) )
			return Float( raw, at );
		if ( type == typeof( Vector3 ) )
			return new Vector3( Float( raw, at ), Float( raw, at + 4 ), Float( raw, at + 8 ) );
		return new Vector4( Float( raw, at ), Float( raw, at + 4 ), Float( raw, at + 8 ), Float( raw, at + 12 ) );
	}

	private static void Store( byte[] raw, int at, object value )
	{
		void F( int offset, float v ) => BinaryPrimitives.WriteInt32BigEndian( raw.AsSpan( at + offset, 4 ), BitConverter.SingleToInt32Bits( v ) );
		switch ( value )
		{
			case byte v: raw[ at ] = v; break;
			case int v: BinaryPrimitives.WriteInt32BigEndian( raw.AsSpan( at, 4 ), v ); break;
			case float v: F( 0, v ); break;
			case Vector3 v: F( 0, v.X ); F( 4, v.Y ); F( 8, v.Z ); break;
			case Vector4 v: F( 0, v.X ); F( 4, v.Y ); F( 8, v.Z ); F( 12, v.W ); break;
		}
	}
}
