namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Owns the ordinary support/sample services for an explicitly selected mesh-world source.</summary>
internal sealed class PhysicalGroundMeshQueryBatch
{
	private readonly TrajectoryQueryRequest[] _support = new TrajectoryQueryRequest[ PhysicalGroundQueryRequests.SupportCapacity ];
	private readonly MeshQueryRequest[] _samples = new MeshQueryRequest[ PhysicalGroundQueryRequests.SampleCapacity ];
	private readonly uint[][] _supportResults = { new uint[ 35 ], new uint[ 35 ], new uint[ 35 ] };
	private readonly uint[][] _sampleResults = new uint[ PhysicalGroundQueryRequests.SampleCapacity ][];
	private readonly bool[] _supportValid = new bool[ PhysicalGroundQueryRequests.SupportCapacity ];
	private readonly bool[] _sampleValid = new bool[ PhysicalGroundQueryRequests.SampleCapacity ];
	internal PhysicalGroundQueryResults Results { get; }
	internal int SupportState { get; private set; }
	internal int SampleState { get; private set; }
	internal int SampleCount { get; private set; }
	internal ReadOnlySpan<TrajectoryQueryRequest> SupportRequests => _support;
	internal ReadOnlySpan<MeshQueryRequest> SampleRequests => _samples.AsSpan( 0, SampleCount );

	internal PhysicalGroundMeshQueryBatch( Func<Vector4, Vector4, uint, int, ReadOnlyMemory<uint>> registeredEdges )
	{
		for ( var index = 0; index < _sampleResults.Length; index++ )
			_sampleResults[ index ] = new uint[ 8 ];
		Results = new( () => SupportState = 0, () => SampleState = 0, GetSupport, GetSample, registeredEdges );
	}

	/// <summary>Issues and completes both workers synchronously; result publication remains a later owner phase.</summary>
	internal void Issue( Span<uint> owner, ReadOnlySpan<uint> frame, MeshWorldQuery mesh,
		TrajectoryWorldQuery trajectory, MeshQueryInstance[] instances )
	{
		ArgumentNullException.ThrowIfNull( mesh );
		ArgumentNullException.ThrowIfNull( trajectory );
		ArgumentNullException.ThrowIfNull( instances );
		PhysicalGroundQueryRequests.Transform( owner, frame );
		SupportState = 1;
		SampleState = 1;
		SampleCount = 0;
		var filter = unchecked((int)owner[ 0x6298 / 4 ]);
		var surfaces = new MeshQueryFilter( 0, 0, 0, 0, true, true );
		for ( var index = 0; index < _support.Length; index++ )
		{
			var offset = 0x2950 + index * 80;
			var start = PhysicalGroundQueryRequests.Read( owner, offset + 32 );
			var end = PhysicalGroundQueryRequests.Read( owner, offset + 48 );
			_support[ index ] = new( Xyz( start ), Xyz( end - start ), Vector3.Zero, 1f,
				PhysicalGroundQueryRequests.Float( owner, offset + 64 ), 0f, 0f, filter, 0x6000, surfaces )
			{
				PositionAuxiliary = start.W,
				VelocityAuxiliary = end.W - start.W
			};
		}
		for ( var index = 0; index < owner[ 0x2a44 / 4 ]; index++ )
			AddSample( owner, 0x1550 + index * 80, false, filter, surfaces );
		for ( var index = 0; index < owner[ 0x2a40 / 4 ]; index++ )
		{
			var offset = 0x150 + index * 80;
			AddSample( owner, offset, false, filter, surfaces );
			if ( unchecked((int)owner[ (offset + 72) / 4 ]) >= 0 )
				AddSample( owner, offset, true, filter, surfaces );
		}
		SupportState = 2;
		for ( var index = 0; index < _support.Length; index++ )
		{
			var hit = trajectory.Evaluate( _support[ index ], instances );
			_supportValid[ index ] = hit.Sample >= 0;
			if ( !_supportValid[ index ] )
				continue;
			var record = _supportResults[ index ];
			Write( record, 0, hit.Position );
			Write( record, 4, hit.Normal );
			Write( record, 8, hit.SurfaceNormal );
			for ( var lane = 12; lane < 16; lane++ )
				record[ lane ] = BitConverter.SingleToUInt32Bits( hit.Time );
			WriteTransform( record, hit.Transform );
			record[ 32 ] = unchecked((uint)hit.Sample);
			record[ 33 ] = hit.Material;
			record[ 34 ] = hit.Identity;
		}
		SampleState = 2;
		for ( var index = 0; index < SampleCount; index++ )
		{
			var hit = mesh.Evaluate( _samples[ index ], instances );
			_sampleValid[ index ] = hit.HitCount != 0;
			if ( !_sampleValid[ index ] )
				continue;
			Write( _sampleResults[ index ], 0, hit.Position );
			Write( _sampleResults[ index ], 4, hit.Normal );
		}
		owner[ 0x13c / 4 ] = (owner[ 0x13c / 4 ] & 0x00ffffff) | 0x01000000;
	}

	private void AddSample( ReadOnlySpan<uint> owner, int offset, bool reverse, int filter, MeshQueryFilter surfaces )
	{
		// Request IDs include reverse submissions, but the original constructor reserves only 38 slots.
		// Rejected submissions do not renumber subsequent descriptor IDs.
		if ( SampleCount == _samples.Length )
			return;
		var start = PhysicalGroundQueryRequests.Read( owner, offset + (reverse ? 48 : 32) );
		var end = PhysicalGroundQueryRequests.Read( owner, offset + (reverse ? 32 : 48) );
		_samples[ SampleCount++ ] = new( Xyz( start ), Xyz( end ),
			PhysicalGroundQueryRequests.Float( owner, offset + 64 ), filter, 0x6000, surfaces );
	}

	private ReadOnlyMemory<uint> GetSupport( uint index ) => index < _supportResults.Length && _supportValid[ index ]
		? _supportResults[ index ] : ReadOnlyMemory<uint>.Empty;

	private ReadOnlyMemory<uint> GetSample( uint index ) => index < SampleCount && _sampleValid[ index ]
		? _sampleResults[ index ] : ReadOnlyMemory<uint>.Empty;

	private static Vector3 Xyz( Vector4 value ) => new( value.X, value.Y, value.Z );

	private static void Write( Span<uint> words, int offset, Vector3 value )
	{
		words[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		words[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		words[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		words[ offset + 3 ] = 0;
	}

	private static void WriteTransform( Span<uint> words, Matrix4x4 value )
	{
		Span<float> elements = stackalloc float[]
		{
			value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
			value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44
		};
		for ( var index = 0; index < elements.Length; index++ )
			words[ 16 + index ] = BitConverter.SingleToUInt32Bits( elements[ index ] );
	}
}
