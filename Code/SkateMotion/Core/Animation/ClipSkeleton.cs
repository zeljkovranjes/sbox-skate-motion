using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Bone hierarchy and rest pose shared by every clip in a set. Positions are metres, Z up, X forward.</summary>
internal sealed class ClipSkeleton
{
	private readonly Dictionary<string, int> _indices = new( StringComparer.OrdinalIgnoreCase );

	internal ClipSkeleton( string[] names, int[] parents, Vector3[] restPositions, Quaternion[] restRotations )
	{
		if ( parents.Length != names.Length || restPositions.Length != names.Length || restRotations.Length != names.Length )
			throw new ArgumentException( "Skeleton arrays must have one entry per bone." );
		for ( var i = 0; i < names.Length; i++ )
		{
			if ( parents[ i ] >= i )
				throw new ArgumentException( "Each bone must follow its parent." );
			_indices[ names[ i ] ] = i;
		}
		Names = names;
		Parents = parents;
		RestPositions = restPositions;
		RestRotations = restRotations;
	}

	internal string[] Names { get; }
	internal int[] Parents { get; }
	internal Vector3[] RestPositions { get; }
	internal Quaternion[] RestRotations { get; }
	internal int Count => Names.Length;

	internal int IndexOf( string name ) => _indices.TryGetValue( name, out var index ) ? index : -1;
}
