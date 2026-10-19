namespace SkateMotion.Core.Collision;

/// <summary>Surface exclusion words and allowed front/back approach directions.</summary>
internal readonly record struct MeshQueryFilter(
	uint Surface0, uint Surface1, uint Surface2, uint Surface3, bool Front, bool Back )
{
	internal bool Excludes( ushort material )
	{
		var audio = material & 127;
		var word = (audio >> 5) switch
		{
			0 => Surface0,
			1 => Surface1,
			2 => Surface2,
			_ => Surface3
		};
		// The last word serves both the highest audio range and physical materials.
		return (word & (1u << (audio & 31))) != 0
			|| (Surface3 & (1u << ((material >> 7) & 31))) != 0;
	}
}
