using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

internal sealed partial class PhysicalSolverWorld
{
	internal ContactConstraint CompileGeneratedRecord( ReadOnlySpan<uint> record,
		ReadOnlySpan<GeneratedContactBodyBinding> bindings, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		if ( record.Length < GeneratedContactRecord.WordCount )
			throw new ArgumentException( "A generated contact requires a complete record.", nameof( record ) );
		var bodyA = -1;
		var bodyB = -1;
		for ( var i = 0; i < bindings.Length; ++i )
		{
			var binding = bindings[ i ];
			ArgumentNullException.ThrowIfNull( binding.Body );
			var index = IndexOfBody( binding.Body );
			if ( index < 0 )
				throw new ArgumentException( "Every generated contact binding must belong to this world.", nameof( bindings ) );
			for ( var j = 0; j < i; ++j )
				if ( bindings[ j ].Identity == binding.Identity )
					throw new ArgumentException( "Generated contact identities must have one binding.", nameof( bindings ) );
			if ( binding.Identity == record[ 3 ] )
				bodyA = index;
			if ( binding.Identity == record[ 7 ] )
				bodyB = index;
		}
		if ( bodyA < 0 || bodyB < 0 )
			throw new ArgumentException( "Both generated contact identities require an explicit binding.", nameof( bindings ) );
		var inputs = GeneratedContactRecord.Decode( record, bodyA, bodyB );
		return ContactCompiler.Compile( inputs.A, inputs.B, inputs.Geometry, step );
	}
}
