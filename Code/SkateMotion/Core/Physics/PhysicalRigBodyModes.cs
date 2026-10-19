namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Applies the body, damping and collision portion of a canonical rig mode transition.</summary>
internal sealed class PhysicalRigBodyModes
{
	private const float DampingScale = 59.999996185302734f;
	internal PhysicalRigModeBody[] Bodies { get; }
	internal Vector3[] ContactProperties { get; }
	internal PhysicalRigPairMask PairMask { get; private set; }
	internal uint AggregateFlags { get; private set; }
	internal bool PrimaryPending { get; set; }
	internal bool Secondary { get; private set; }

	internal PhysicalRigBodyModes( PhysicalRigModeBody[] bodies, Vector3[] contactProperties, bool suppressAll )
	{
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( contactProperties );
		if ( bodies.Length != 26 || contactProperties.Length != 3 )
			throw new ArgumentException( "Rig modes require 26 bodies and three contact property records." );
		for ( var i = 0; i < bodies.Length; ++i )
		{
			ArgumentNullException.ThrowIfNull( bodies[ i ] );
			for ( var j = 0; j < i; ++j )
				if ( ReferenceEquals( bodies[ i ].Body, bodies[ j ].Body ) )
					throw new ArgumentException( "Canonical bodies require distinct physical identities." );
		}
		Bodies = bodies.AsSpan().ToArray();
		ContactProperties = contactProperties.AsSpan().ToArray();
		PairMask = new( false, suppressAll );
	}

	internal void ApplyPrimary( bool enableCollisions )
	{
		SetAggregate( false, 5, 0f, 0f );
		for ( var i = 1; i < 24; ++i )
		{
			var body = Bodies[ i ];
			body.ContactPropertySlot = 0;
			body.RefreshMass( body.Primary, true );
			if ( enableCollisions )
			{
				if ( body.CollisionCounter == 0 )
					body.ShapeMode = 0;
			}
			else
			{
				body.ShapeFlags &= ~1u;
				body.CollisionCounter = 0;
			}
		}
		Bodies[ 0 ].ShapeMode = 4;
		Bodies[ 0 ].ShapeFlags &= ~1u;
		Bodies[ 0 ].CollisionCounter = 0;
		if ( !enableCollisions )
		{
			Bodies[ 24 ].ShapeFlags &= ~1u;
			Bodies[ 25 ].ShapeFlags &= ~1u;
		}
	}

	internal void ApplySecondary( PhysicalRigSecondaryMode settings )
	{
		ContactProperties[ 1 ] = ContactProperties[ 1 ] with { Z = settings.SpecialContactResponse ? .99f : settings.ContactResponse };
		ContactProperties[ 2 ] = ContactProperties[ 2 ] with { Z = settings.SpecialContactResponse ? .99f : settings.SpecialResponse };
		SetAggregate( true, 6, settings.LinearDamping, settings.AngularDamping );
		for ( var i = 1; i < 24; ++i )
		{
			var body = Bodies[ i ];
			body.CollisionCounter = 0;
			body.ShapeFlags |= 1u;
			body.ContactPropertySlot = i is 1 or 3 or 7 ? 2 : 1;
			var mass = body.Body.Mass.Value;
			body.RefreshMass( new(
				settings.AlternateInertia ? body.Alternate.InverseInertia : mass.InverseInertia,
				settings.AlternateMass ? body.Alternate.InverseMass : mass.InverseMass ), settings.AlternateInertia );
		}
		PrimaryPending = false;
	}

	internal void AdvanceCollisionCounters()
	{
		if ( !PrimaryPending )
			return;
		var pending = false;
		for ( var i = 0; i < 24; ++i )
		{
			var body = Bodies[ i ];
			// The retained word is tested as a signed count by the producer.
			if ( unchecked( (int)body.CollisionCounter ) <= 0 )
				continue;
			--body.CollisionCounter;
			if ( body.CollisionCounter == 0 )
				body.ShapeFlags |= 1u;
			else
			{
				body.ShapeFlags &= ~1u;
				pending = true;
			}
		}
		PrimaryPending = pending;
	}

	private void SetAggregate( bool secondary, uint flags, float linearDamping, float angularDamping )
	{
		Secondary = secondary;
		PairMask = PairMask with { Secondary = secondary };
		AggregateFlags = flags;
		foreach ( var body in Bodies )
		{
			body.ShapeFlags = flags;
			body.LinearDamping = linearDamping * DampingScale;
			body.AngularDamping = angularDamping * DampingScale;
		}
	}
}
