using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

internal sealed partial class ArticulatedBoard
{
	internal int StepGenerated( ReadOnlySpan<uint> records, ReadOnlySpan<BoardContactBodyBinding> bindings,
		ReadOnlySpan<CollisionContact> genericContacts, float steeringAngle, float brake, float step )
	{
		if ( records.Length % GeneratedContactRecord.WordCount != 0 )
			throw new ArgumentException( "Generated contacts must contain complete records.", nameof( records ) );
		if ( genericContacts.Length > ContactCapacity )
			throw new ArgumentException( "Too many generic board contacts.", nameof( genericContacts ) );
		ValidateBindings( bindings );
		var generatedCount = records.Length / GeneratedContactRecord.WordCount;
		var total = checked(generatedCount + genericContacts.Length);
		if ( total > _contacts.Length )
			_contacts = new ContactConstraint[ Math.Max( total, checked(_contacts.Length * 2) ) ];
		PrepareWorkspace();
		for ( var i = 0; i < generatedCount; ++i )
		{
			var record = records.Slice( i * GeneratedContactRecord.WordCount, GeneratedContactRecord.WordCount );
			var bodyA = ResolveBody( record[ 3 ], bindings );
			var bodyB = ResolveBody( record[ 7 ], bindings );
			var input = GeneratedContactRecord.Decode( record, bodyA, bodyB );
			_contacts[ i ] = ContactCompiler.Compile( input.A, input.B, input.Geometry, step );
		}
		CompileGenericContacts( genericContacts, generatedCount, step );
		CompleteStep( total, steeringAngle, brake, step, generatedCount );
		return total;
	}

	private void ValidateBindings( ReadOnlySpan<BoardContactBodyBinding> bindings )
	{
		for ( var i = 0; i < bindings.Length; ++i )
		{
			if ( (uint)bindings[ i ].BodyIndex >= (uint)_bodies.Length )
				throw new ArgumentOutOfRangeException( nameof( bindings ) );
			for ( var j = 0; j < i; ++j )
				if ( bindings[ j ].Identity == bindings[ i ].Identity )
					throw new ArgumentException( "Generated contact identities require unique bindings.", nameof( bindings ) );
		}
	}

	private static int ResolveBody( uint identity, ReadOnlySpan<BoardContactBodyBinding> bindings )
	{
		foreach ( var binding in bindings )
			if ( binding.Identity == identity )
				return binding.BodyIndex;
		throw new ArgumentException( "Each generated contact identity requires an explicit body binding.", nameof( bindings ) );
	}
}
