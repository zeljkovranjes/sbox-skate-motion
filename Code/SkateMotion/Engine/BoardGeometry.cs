using Sandbox;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Engine;

internal static class BoardGeometry
{
	internal static Model Deck()
	{
		var vertices = new VertexBuffer();
		vertices.Init( true );
		for ( var i = 0; i < 32; i++ )
		{
			var a = Outline( i );
			var b = Outline( i + 1 );
			var bottomA = a - Vector3.Up * Thickness;
			var bottomB = b - Vector3.Up * Thickness;
			Triangle( vertices, new( 0f, 0f, CentreTop ), a, b, new Color( 0.08f, 0.1f, 0.12f ) );
			Triangle( vertices, new( 0f, 0f, CentreBottom ), bottomB, bottomA, new Color( 0.15f, 0.55f, 0.65f ) );
			Triangle( vertices, a, bottomA, bottomB, new Color( 0.65f, 0.45f, 0.24f ) );
			Triangle( vertices, a, bottomB, b, new Color( 0.65f, 0.45f, 0.24f ) );
		}
		return Build( vertices, "Skate Motion deck" );
	}

	internal static Model Wheel( float radius )
	{
		var vertices = new VertexBuffer();
		vertices.Init( true );
		const float halfWidth = 0.011f;
		for ( var i = 0; i < 24; i++ )
		{
			var a = Circle( i, radius );
			var b = Circle( i + 1, radius );
			var leftA = a.WithY( halfWidth );
			var leftB = b.WithY( halfWidth );
			var rightA = a.WithY( -halfWidth );
			var rightB = b.WithY( -halfWidth );
			var color = i < 3 ? new Color( 0.9f, 0.4f, 0.12f ) : new Color( 0.9f, 0.85f, 0.7f );
			Triangle( vertices, leftA, leftB, rightB, color );
			Triangle( vertices, leftA, rightB, rightA, color );
			Triangle( vertices, new( 0f, halfWidth, 0f ), leftB, leftA, color );
			Triangle( vertices, new( 0f, -halfWidth, 0f ), rightA, rightB, color );
		}
		return Build( vertices, "Skate Motion wheel" );
	}

	/// <param name="track">Axle length, metres.</param>
	/// <param name="rise">Height from the axle up to the deck's underside, metres: the hanger, kingpin and baseplate
	/// fill it so the wheels visibly hang from the deck.</param>
	/// <param name="baseplate">Draw the baseplate with the truck; without it, it is drawn on the deck (see <see cref="Baseplate"/>)
	/// and leans with it.</param>
	internal static Model Truck( float track, float rise, bool baseplate = true )
	{
		var vertices = new VertexBuffer();
		vertices.Init( true );
		var metal = new Color( 0.45f, 0.48f, 0.5f );
		// The axle is drawn as thick as the hanger a rail contact rests on, so a grinding truck is drawn on the rail.
		Box( vertices, new Vector3( 0f, 0f, 0f ), new Vector3( 0.025f, track, RailContacts.AxleRadius * 2f ), metal );
		if ( rise > FullTruckRise )
		{
			Box( vertices, new Vector3( 0f, 0f, 0.012f ), new Vector3( 0.035f, track * 0.7f, 0.03f ), metal );
			var column = rise - 0.008f - 0.027f;
			Box( vertices, new Vector3( 0f, 0f, 0.027f + column * 0.5f ), new Vector3( 0.03f, 0.035f, column ),
				new Color( 0.75f, 0.2f, 0.15f ) );
			if ( baseplate )
				Box( vertices, new Vector3( 0f, 0f, rise - 0.004f ), BaseplateSize, metal );
		}
		return Build( vertices, "Skate Motion truck" );
	}

	/// <summary>The trucks drawn with a deck rise at least this high (metres) have a hanger, kingpin and baseplate.</summary>
	internal const float FullTruckRise = 0.04f;

	private static readonly Vector3 BaseplateSize = new( 0.07f, 0.055f, 0.008f );

	/// <summary>A truck's baseplate on its own, about its middle: fixed to the deck's underside, it leans with the deck.</summary>
	internal static Model Baseplate()
	{
		var vertices = new VertexBuffer();
		vertices.Init( true );
		Box( vertices, Vector3.Zero, BaseplateSize, new Color( 0.45f, 0.48f, 0.5f ) );
		return Build( vertices, "Skate Motion baseplate" );
	}

	private static void Box( VertexBuffer vertices, Vector3 center, Vector3 size, Color color ) =>
		vertices.AddCube( center * SkateUnits.InchesPerMetre, size * SkateUnits.InchesPerMetre, Rotation.Identity, color );

	/// <summary>
	/// Top and underside of the generated deck at a point of its own frame (metres, X along the deck), or null off
	/// it. Procedural models cannot be traced, so the deck's shape answers directly: its top fans from the centre
	/// to a rim that rises toward the kicktails, and its underside lies 15 mm below the rim.
	/// </summary>
	internal static (float Top, float Bottom)? DeckSurface( float x, float y )
	{
		var along = MathF.Abs( x ) - HalfStraight;
		var outside = along > 0f ? along * along + y * y : y * y;
		if ( outside > Radius * Radius )
			return null;
		// Scale the point out to the rim along its direction from the centre; the fan is linear in between.
		var reach = 0f;
		for ( var step = 0.5f; step > 0.0001f; step *= 0.5f )
		{
			var scale = reach + step;
			var sx = MathF.Abs( x ) * (1f / scale) - HalfStraight;
			var sy = y * (1f / scale);
			if ( (sx > 0f ? sx * sx + sy * sy : sy * sy) <= Radius * Radius )
				continue;
			reach = scale;
		}
		var share = Math.Clamp( reach + 0.0001f, 0f, 1f );
		var rimX = share > 0f ? MathF.Abs( x ) / share : 0f;
		var rim = RimHeight( rimX );
		var top = CentreTop + share * (rim - CentreTop);
		var bottom = CentreBottom + share * (rim - Thickness - CentreBottom);
		return (top, bottom);
	}

	/// <summary>The generated deck's length, width and thickness (metres).</summary>
	internal const float DeckLength = 2f * (HalfStraight + Radius);
	internal const float DeckWidth = 2f * Radius;
	internal const float DeckThickness = CentreTop - CentreBottom;

	private const float HalfStraight = 0.29f;
	private const float Radius = 0.11f;
	private const float CentreTop = 0.008f;
	private const float CentreBottom = -0.007f;
	private const float Thickness = 0.015f;

	private static float RimHeight( float x ) => CentreTop + Math.Max( Math.Abs( x ) - 0.28f, 0f ) * 0.2f;

	private static Vector3 Outline( int index )
	{
		var angle = index * MathF.PI / 16f;
		var cosine = MathF.Cos( angle );
		var x = (cosine >= 0f ? HalfStraight : -HalfStraight) + cosine * Radius;
		return new( x, MathF.Sin( angle ) * Radius, RimHeight( x ) );
	}

	private static Vector3 Circle( int index, float radius )
	{
		var angle = index * MathF.PI / 12f;
		return new( MathF.Cos( angle ) * radius, 0f, MathF.Sin( angle ) * radius );
	}

	private static void Triangle( VertexBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Color color )
	{
		var normal = Vector3.Cross( b - a, c - a ).Normal;
		var tangent = (b - a).Normal;
		buffer.AddTriangle(
			new Vertex( a * SkateUnits.InchesPerMetre, normal, tangent, Vector4.Zero ) { Color = color },
			new Vertex( b * SkateUnits.InchesPerMetre, normal, tangent, Vector4.Zero ) { Color = color },
			new Vertex( c * SkateUnits.InchesPerMetre, normal, tangent, Vector4.Zero ) { Color = color } );
	}

	private static Model Build( VertexBuffer vertices, string name )
	{
		var mesh = new Mesh( Material.Load( "materials/default/vertex_color.vmat" ) );
		mesh.CreateBuffers( vertices );
		return Model.Builder.AddMesh( mesh ).WithName( name ).Create();
	}
}
