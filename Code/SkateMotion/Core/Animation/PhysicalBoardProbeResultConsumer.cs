namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Publishes completed wheel observations and updates retained ground and forward probe caches.</summary>
internal static class PhysicalBoardProbeResultConsumer
{
	internal static void Update( PhysicalBoardProbeState probes, PhysicalBoardProbeContactState contacts,
		IPhysicalBoardProbeResults results )
	{
		ArgumentNullException.ThrowIfNull( probes );
		ArgumentNullException.ThrowIfNull( contacts );
		ArgumentNullException.ThrowIfNull( results );
		results.Complete();
		contacts.ClosestDistance = .2f;
		for ( var index = 0; index < 4; index++ )
		{
			contacts.Normals[ index ] = Vector3.UnitY;
			contacts.Distances[ index ] = 0f;
			contacts.SurfaceIndices[ index ] = 0;
			contacts.MaterialIndices[ index ] = 0;
			contacts.Flags[ index ] = 0;
			if ( !results.TryGet( index, out var hit ) )
				continue;
			var distance = hit.Fraction * .2f;
			contacts.ClosestDistance = distance - contacts.ClosestDistance >= 0f ? contacts.ClosestDistance : distance;
			contacts.Normals[ index ] = hit.Normal;
			contacts.Distances[ index ] = distance;
			contacts.SurfaceIndices[ index ] = (hit.PackedMaterial >> 7) & 31U;
			contacts.MaterialIndices[ index ] = hit.PackedMaterial & 127U;
			contacts.Flags[ index ] = (hit.PackedMaterial >> 12) & 15U;
		}
		Publish( probes.Ground, results, 4 );
		if ( (probes.SkeletonFlags & (1UL << 61)) != 0 )
			Publish( probes.Forward, results, 5 );
	}

	private static void Publish( PhysicalBoardProbeCache cache, IPhysicalBoardProbeResults results, int index )
	{
		cache.Valid = results.TryGet( index, out var hit );
		if ( !cache.Valid )
			return;
		cache.Position = hit.Position;
		cache.Normal = hit.Normal;
		cache.Material = unchecked((int)hit.PackedMaterial);
	}
}
