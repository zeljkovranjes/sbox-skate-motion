namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal static class WheelSuspension
{
	internal static void Solve( in BoardBody body, ReadOnlySpan<WheelContact> contacts,
		SkateSettings settings, float step, Span<float> loads )
	{
		Span<float> matrix = stackalloc float[ 16 ];
		Span<float> target = stackalloc float[ 4 ];
		Span<bool> active = stackalloc bool[ 4 ];
		loads.Clear();
		var damping = settings.Damping + settings.Spring * step;
		for ( var i = 0; i < 4; i++ )
		{
			// Reuse the same geometry and finite-value validation as the tire solver.
			active[ i ] = SkateWheels.Solve( body, contacts[ i ], i, default,
				settings, step, 0f, 0f, 1f ).Load > 0f;
		}
		for ( var i = 0; i < 4; i++ )
		{
			if ( !active[ i ] )
				continue;
			var normal = Vector3.Normalize( contacts[ i ].Normal );
			var speed = Vector3.Dot( body.PointVelocity( contacts[ i ].Point ) - contacts[ i ].SurfaceVelocity, normal );
			// Solve the four springs together: a force at one wheel also accelerates the others.
			// Include gravity in the predicted normal velocity, preserving equilibrium as dt changes.
			target[ i ] = settings.Spring * (settings.RideHeight - contacts[ i ].Distance)
				- damping * (speed - settings.Gravity * step * normal.Z);
			for ( var j = 0; j < 4; j++ )
			{
				if ( active[ j ] )
					matrix[ i * 4 + j ] = (i == j ? 1f : 0f) + damping * step
						* body.ContactCoupling( contacts[ i ].Point, normal,
							contacts[ j ].Point, Vector3.Normalize( contacts[ j ].Normal ), settings.Mass );
			}
		}
		// Projected Gauss-Seidel solves this positive-definite compliant contact system.
		// The fixed four-contact bound avoids allocations and unbounded work per board.
		for ( var iteration = 0; iteration < 32; iteration++ )
		{
			for ( var i = 0; i < 4; i++ )
			{
				if ( !active[ i ] )
					continue;
				var remainder = target[ i ];
				for ( var j = 0; j < 4; j++ )
				{
					if ( i != j )
						remainder -= matrix[ i * 4 + j ] * loads[ j ];
				}
				loads[ i ] = Math.Clamp( remainder / matrix[ i * 4 + i ], 0f, settings.Mass * settings.Gravity * 3f );
			}
		}
	}
}
