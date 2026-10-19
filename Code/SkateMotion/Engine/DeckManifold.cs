using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core;

namespace SkateMotion.Engine;

using NumericsVector = System.Numerics.Vector3;
using NumericsRotation = System.Numerics.Quaternion;

internal sealed class DeckManifold
{
	private readonly Rigidbody _deck;
	private readonly NumericsVector[] _vertices;
	private readonly DeckHit[] _hits = new DeckHit[ 8 ];
	private NumericsVector _normal = NumericsVector.UnitZ;
	private NumericsVector _previousCenter;
	private bool _havePreviousCenter;
	private readonly bool _ignoreDynamic;
	internal bool HasDynamicObstruction { get; private set; }

	internal DeckManifold( Rigidbody deck, bool ignoreDynamic = false )
	{
		_deck = deck;
		_ignoreDynamic = ignoreDynamic;
		var vertices = new List<NumericsVector>();
		foreach ( var shape in deck.PhysicsBody.Shapes )
		{
			if ( shape.IsTrigger || shape.IsMeshShape )
				continue;
			shape.Triangulate( out var positions, out var indices );
			foreach ( var point in positions )
			{
				var local = SkateUnits.Metres( point );
				if ( !vertices.Contains( local ) )
					vertices.Add( local );
			}
		}
		_vertices = vertices.ToArray();
	}

	internal void Reset( NumericsVector up )
	{
		_normal = up;
		_havePreviousCenter = false;
	}

	internal ReadOnlySpan<DeckHit> Sample( BoardBody body, NumericsVector acceleration, float step, bool recover )
	{
		var position = body.Position + (body.Velocity + acceleration * step) * step;
		var delta = new NumericsRotation( body.AngularVelocity * (step * 0.5f), 0f ) * body.Rotation;
		var rotation = NumericsRotation.Normalize( body.Rotation + delta );
		var count = SampleSwept( body, position, rotation, recover );
		return _hits.AsSpan( 0, InteriorContacts( body, position, rotation, count ) );
	}

	private int SampleSwept( BoardBody body, NumericsVector position, NumericsRotation rotation, bool recover )
	{
		var previousCenter = _havePreviousCenter ? _previousCenter : body.Position;
		_previousCenter = body.Position;
		_havePreviousCenter = true;
		var from = new Transform( SkateUnits.Inches( body.Position ), SkateUnits.ToEngine( body.Rotation ) );
		var to = new Transform( SkateUnits.Inches( position ), SkateUnits.ToEngine( rotation ) );
		var trace = Sweep( from, to );
		HasDynamicObstruction = trace.Body.IsValid() && trace.Body.MotionEnabled;
		if ( trace.StartedSolid || (!trace.Hit && recover) )
		{
			if ( trace.StartedSolid && trace.Body.IsValid() && trace.Body.MotionEnabled
				&& trace.Body.ComputePenetration( _deck.PhysicsBody, from, out var direction, out var distance ) )
			{
				// A moving prop may enter between native ticks. Query its actual separating
				// face, rather than lifting the board and misreading the prop's top as ground.
				from.Position -= direction * (distance + 0.1f);
			}
			else
			{
				// Mesh penetration queries do not provide a separating vector in this engine.
				from.Position += SkateUnits.Inches( _normal * 0.15f );
			}
			trace = Sweep( from, to );
		}
		if ( !trace.Hit || trace.StartedSolid )
			return VertexContacts( body, position, rotation );
		_normal = SkateUnits.ToCore( trace.Normal );
		var plane = SkateUnits.Metres( trace.HitPosition );
		if ( MeshFaces.IsBackFace( _deck.Scene, trace, _deck.GameObject )
			|| (!recover && trace.Shape.IsValid() && trace.Shape.IsMeshShape
				&& NumericsVector.Dot( previousCenter - plane, _normal ) < -0.001f) )
			return VertexContacts( body, position, rotation );
		var minimum = float.MaxValue;
		foreach ( var vertex in _vertices )
			minimum = Math.Min( minimum, NumericsVector.Dot( position + NumericsVector.Transform( vertex, rotation ) - plane, _normal ) );
		var count = 0;
		foreach ( var vertex in _vertices )
		{
			var end = position + NumericsVector.Transform( vertex, rotation );
			if ( NumericsVector.Dot( end - plane, _normal ) > minimum + 0.004f )
				continue;
			var point = body.LocalPoint( vertex );
			var gap = NumericsVector.Dot( point - plane, _normal );
			var start = point + _normal * (Math.Max( -gap, 0f ) + 0.03f);
			var query = _deck.Scene.Trace.Ray( SkateUnits.Inches( start ), SkateUnits.Inches( end - _normal * 0.03f ) )
				.UseHitPosition().IgnoreGameObjectHierarchy( _deck.GameObject ).WithoutTags( SkateTags.Rider );
			var hit = (_ignoreDynamic ? query.IgnoreDynamic() : query).Run();
			if ( !hit.Hit || hit.StartedSolid || Vector3.Dot( hit.Normal, trace.Normal ) < 0.9f )
				continue;
			_hits[ count++ ] = new( SkateUnits.Inches( point ), hit );
			if ( count == _hits.Length )
				break;
		}
		if ( count == 0 )
		{
			var hitRotation = NumericsRotation.Slerp( body.Rotation, rotation, trace.Fraction );
			var hitCenter = Vector3.Lerp( from.Position, to.Position, trace.Fraction );
			var local = NumericsVector.Transform( plane - SkateUnits.Metres( hitCenter ), NumericsRotation.Conjugate( hitRotation ) );
			_hits[ count++ ] = new( SkateUnits.Inches( body.LocalPoint( local ) ), trace );
		}
		return count;
	}

	private int InteriorContacts( BoardBody body, NumericsVector position, NumericsRotation rotation, int count )
	{
		// A corner can come to rest past a zero-thickness face after tumbling; swept queries
		// no longer see that face. A face crossed between the deck center and a corner is a contact.
		var center = SkateUnits.Inches( position );
		foreach ( var vertex in _vertices )
		{
			if ( count == _hits.Length )
				break;
			var end = position + NumericsVector.Transform( vertex, rotation );
			// Static geometry only: moving bodies are resolved by the sweep above.
			var hit = _deck.Scene.Trace.Ray( center, SkateUnits.Inches( end ) )
				.UseHitPosition().IgnoreGameObjectHierarchy( _deck.GameObject ).WithoutTags( SkateTags.Rider ).IgnoreDynamic().Run();
			if ( !hit.Hit || hit.StartedSolid || Covered( hit, count ) )
				continue;
			_hits[ count++ ] = new( SkateUnits.Inches( body.LocalPoint( vertex ) ), hit );
		}
		return count;
	}

	private bool Covered( SceneTraceResult hit, int count )
	{
		for ( var i = 0; i < count; i++ )
		{
			// The swept manifold already resolves this face with its support vertices.
			if ( _hits[ i ].Trace.Body == hit.Body && Vector3.Dot( _hits[ i ].Trace.Normal, hit.Normal ) > 0.9f )
				return true;
		}
		return false;
	}

	private int VertexContacts( BoardBody body, NumericsVector position, NumericsRotation rotation )
	{
		// A hull sweep can begin straddling a thin mesh during rotation. Trace each
		// actual vertex's motion so a rejected back face cannot erase a front-face hit.
		var count = 0;
		foreach ( var vertex in _vertices )
		{
			var point = body.LocalPoint( vertex );
			var end = position + NumericsVector.Transform( vertex, rotation );
			if ( NumericsVector.DistanceSquared( point, end ) < 0.0000000001f ) continue;
			var query = _deck.Scene.Trace.Ray( SkateUnits.Inches( point ), SkateUnits.Inches( end ) )
				.UseHitPosition().IgnoreGameObjectHierarchy( _deck.GameObject ).WithoutTags( SkateTags.Rider );
			var hit = (_ignoreDynamic ? query.IgnoreDynamic() : query).Run();
			if ( !hit.Hit || hit.StartedSolid ) continue;
			if ( hit.Shape.IsValid() && hit.Shape.IsMeshShape
				&& NumericsVector.Dot( body.Position - SkateUnits.Metres( hit.HitPosition ), SkateUnits.ToCore( hit.Normal ) ) < -0.001f )
				continue;
			HasDynamicObstruction |= hit.Body.IsValid() && hit.Body.MotionEnabled;
			_hits[ count++ ] = new( SkateUnits.Inches( point ), hit );
			if ( count == _hits.Length ) break;
		}
		return count;
	}

	private SceneTraceResult Sweep( Transform from, Transform to )
	{
		var query = _deck.Scene.Trace.Sweep( _deck, from, to )
			.UseHitPosition().IgnoreGameObjectHierarchy( _deck.GameObject ).WithoutTags( SkateTags.Rider );
		return (_ignoreDynamic ? query.IgnoreDynamic() : query).Run();
	}
}
