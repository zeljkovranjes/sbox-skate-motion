namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Builds solver parentage and bind transforms after the asset adapter resolves bone names.</summary>
internal static class EffectorSkeletonInitializer
{
	internal static EffectorSkeletonHierarchy Create( int[] clipParents, int[] solverToClip,
		Quaternion[] bindRotations, Vector3[] bindPositions, LimbRigidTransform[] clipBones )
	{
		Validate( clipParents, solverToClip, bindRotations, bindPositions, clipBones );
		var count = solverToClip.Length;
		var mapping = new int[ count ];
		Array.Copy( solverToClip, mapping, count );
		var reverse = new int[ clipParents.Length ];
		Array.Fill( reverse, -1 );
		for ( var i = 0; i < count; i++ )
			reverse[ mapping[ i ] ] = i;

		var parents = new int[ count ];
		var edgeIndices = new int[ count ];
		var edgeCount = 0;
		for ( var i = 0; i < count; i++ )
		{
			var parent = clipParents[ mapping[ i ] ];
			while ( parent >= 0 && reverse[ parent ] < 0 )
				parent = clipParents[ parent ];
			parents[ i ] = parent < 0 ? -1 : reverse[ parent ];
			if ( parent >= 0 )
				edgeIndices[ i ] = edgeCount++;
		}

		var edgeChildren = new int[ edgeCount ];
		var edgeParents = new int[ edgeCount ];
		var childCounts = new int[ count ];
		for ( var i = 0; i < count; i++ )
		{
			if ( parents[ i ] < 0 )
				continue;
			edgeChildren[ edgeIndices[ i ] ] = i;
			edgeParents[ edgeIndices[ i ] ] = parents[ i ];
			childCounts[ parents[ i ] ]++;
		}

		var children = new int[ count ][];
		for ( var i = 0; i < count; i++ )
			children[ i ] = new int[ childCounts[ i ] ];
		Array.Clear( childCounts );
		for ( var i = 0; i < count; i++ )
		{
			var parent = parents[ i ];
			if ( parent >= 0 )
				children[ parent ][ childCounts[ parent ]++ ] = i;
		}

		var bind = new LimbRigidTransform[ count ];
		var inverse = new LimbRigidTransform[ count ];
		var initial = new LimbRigidTransform[ count ];
		for ( var i = 0; i < count; i++ )
		{
			bind[ i ] = FromQuaternion( bindRotations[ i ], bindPositions[ i ] );
			var axes = bind[ i ].Rotation.Inverse();
			inverse[ i ] = new LimbRigidTransform( axes, axes.Transform( -bindPositions[ i ], Vector3.Zero ) );
			initial[ i ] = clipBones[ mapping[ i ] ];
		}
		return new EffectorSkeletonHierarchy( mapping, reverse, parents, children, edgeIndices,
			edgeChildren, edgeParents, bind, inverse, initial );
	}

	private static LimbRigidTransform FromQuaternion( Quaternion value, Vector3 position )
	{
		// Scale before products: algebraically equivalent quaternion formulas round differently.
		var scale = MathF.Sqrt( 2f );
		var x = MultiplyAdd( value.X, scale, 0f );
		var y = MultiplyAdd( value.Y, scale, 0f );
		var z = MultiplyAdd( value.Z, scale, 0f );
		var w = MultiplyAdd( value.W, scale, 0f );
		var dx = SubtractProduct( x, x, .5f );
		var dy = SubtractProduct( y, y, .5f );
		var dz = SubtractProduct( z, z, .5f );
		var xy = MultiplyAdd( x, y, 0f );
		var yz = MultiplyAdd( y, z, 0f );
		var zx = MultiplyAdd( z, x, 0f );
		var wx = MultiplyAdd( w, x, 0f );
		var wy = MultiplyAdd( w, y, 0f );
		var wz = MultiplyAdd( w, z, 0f );
		return new LimbRigidTransform( new LimbFrame(
			new Vector3( dy + dz, xy + wz, zx - wy ),
			new Vector3( xy - wz, dz + dx, yz + wx ),
			new Vector3( zx + wy, yz - wx, dx + dy ) ), position );
	}

	private static void Validate( int[] clipParents, int[] solverToClip, Quaternion[] bindRotations,
		Vector3[] bindPositions, LimbRigidTransform[] clipBones )
	{
		if ( clipBones.Length != clipParents.Length || bindRotations.Length != solverToClip.Length
			|| bindPositions.Length != solverToClip.Length )
			throw new ArgumentException( "Clip and solver arrays must match their respective bone counts." );
		var used = new bool[ clipParents.Length ];
		foreach ( var index in solverToClip )
		{
			if ( index < 0 || index >= used.Length || used[ index ] )
				throw new ArgumentException( "Solver bones must map to distinct existing clip bones." );
			used[ index ] = true;
		}
		for ( var i = 0; i < clipParents.Length; i++ )
		{
			var bone = i;
			var remaining = clipParents.Length;
			while ( bone != -1 )
			{
				if ( bone < 0 || bone >= clipParents.Length || remaining-- == 0 )
					throw new ArgumentException( "Clip parents must form an acyclic hierarchy with -1 roots." );
				bone = clipParents[ bone ];
			}
		}
	}
}
