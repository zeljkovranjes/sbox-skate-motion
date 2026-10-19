using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Routes solved constraints into retained owner lists before rider feedback.</summary>
internal sealed class PhysicalWorldContactCollector
{
	private readonly Dictionary<PhysicalBodyState, (PhysicalContactOwner Owner, int Bone)> _registrations = new();
	private readonly HashSet<PhysicalContactOwner> _owners = new();

	internal void Register( PhysicalBodyState body, PhysicalContactOwner owner, int bone )
	{
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentOutOfRangeException.ThrowIfNegative( bone );
		_registrations[ body ] = (owner, bone);
		_owners.Add( owner );
	}

	internal void Unregister( PhysicalBodyState body ) => _registrations.Remove( body );

	internal void Collect( PhysicalWorldContact[] contacts ) => CollectSpan( contacts );

	internal void CollectSpan( ReadOnlySpan<PhysicalWorldContact> contacts )
	{
		foreach ( var contact in contacts )
		{
			ArgumentNullException.ThrowIfNull( contact.FirstBody );
			ArgumentNullException.ThrowIfNull( contact.SecondBody );
		}
		foreach ( var owner in _owners )
			owner.Clear();
		for ( var index = 0; index < contacts.Length; ++index )
		{
			var contact = contacts[ index ];
			_registrations.TryGetValue( contact.FirstBody, out var first );
			_registrations.TryGetValue( contact.SecondBody, out var second );
			if ( first.Owner != null && second.Owner != null &&
				(ReferenceEquals( first.Owner, second.Owner ) ||
				(first.Owner.ActorIdentity != null && ReferenceEquals( first.Owner.ActorIdentity, second.Owner.ActorIdentity ))) )
				continue;
			Append( first.Owner, second.Owner, first.Bone, index, contact, false );
			Append( second.Owner, first.Owner, second.Bone, index, contact, true );
		}
	}

	private static void Append( PhysicalContactOwner owner, PhysicalContactOwner other, int bone,
		int index, PhysicalWorldContact contact, bool second )
	{
		if ( owner == null || owner.Count == PhysicalContactOwner.Capacity )
			return;
		// Adding positive zero preserves the vector multiply-add's signed-zero result.
		var normal = contact.Normal * (second ? -1f : 1f) + Vector4.Zero;
		var velocity = second ? contact.SecondBody.Velocity - contact.FirstBody.Velocity
			: contact.FirstBody.Velocity - contact.SecondBody.Velocity;
		var surface = (int)((contact.Materials >> (second ? 16 : 0)) & 0xffff);
		var rider = new PhysicalRiderContact( bone, new( normal.X, normal.Y, normal.Z ),
			new( contact.Point.X, contact.Point.Y, contact.Point.Z ), surface, other?.Kind ?? 0,
			!second, Body( contact.FirstBody ), Body( contact.SecondBody ), contact.Impulse, other?.Identifier );
		owner.Append( new( index, normal, contact.Point, velocity, other, rider ) );
	}

	private static PhysicalContactBody Body( PhysicalBodyState body ) =>
		new( body.Velocity, body.InverseMass, body.MotionMode == 1 );
}
