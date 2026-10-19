using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

internal sealed partial class PhysicalSolverWorld
{
	internal PhysicalWorldContactCollector ContactCollector { get; } = new();

	internal ContactConstraint CompileGeneratedContact( in ContactManifoldPoint contact, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		var inputs = ContactInputs.FromGeneratedManifold( _bodies[ contact.BodyA ], _bodies[ contact.BodyB ], contact );
		return ContactCompiler.Compile( inputs.A, inputs.B, inputs.Geometry, step );
	}

	internal ContactConstraint CompileManifoldContact( in ContactManifoldPoint contact, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		var inputs = ContactInputs.FromManifold( _bodies[ contact.BodyA ], _bodies[ contact.BodyB ], contact );
		return ContactCompiler.Compile( inputs.A, inputs.B, inputs.Geometry, step );
	}

	internal ContactConstraint CompileContact( in CollisionContact contact, float step )
	{
		if ( !_prepared )
			throw new InvalidOperationException( "Read current physical bodies before compiling their constraints." );
		return ContactCompiler.Compile( _bodies[ contact.BodyA ], _bodies[ contact.BodyB ], contact, step );
	}

	internal int SolveIntegrateAndCollect( Span<ContactConstraint> contacts, Span<JointConstraint> joints,
		Span<DriveConstraint> drives, Span<PhysicalWorldContact> output, int passes, float step, Vector3 gravity )
	{
		return SolveIntegrateAndCollectCore( contacts, joints, drives, output, passes, step, gravity, false );
	}

	private int SolveIntegrateAndCollectCore( Span<ContactConstraint> contacts, Span<JointConstraint> joints,
		Span<DriveConstraint> drives, Span<PhysicalWorldContact> output, int passes, float step, Vector3 gravity,
		bool generated )
	{
		if ( output.Length < contacts.Length )
			throw new ArgumentException( "The contact output buffer must hold the complete solved stream.", nameof( output ) );
		foreach ( var contact in contacts )
			if ( (uint)contact.BodyA >= (uint)_physical.Length || (uint)contact.BodyB >= (uint)_physical.Length )
				throw new ArgumentOutOfRangeException( nameof( contacts ) );
		SolveAndIntegrateCore( contacts, joints, drives, passes, step, gravity, generated );
		var count = 0;
		var inverseStep = 1f / step;
		foreach ( var contact in contacts )
		{
			if ( PhysicalContactOutput.TryExport( contact, _physical[ contact.BodyA ], _physical[ contact.BodyB ],
				inverseStep, out var record ) )
				output[ count++ ] = record;
		}
		ContactCollector.CollectSpan( output[ ..count ] );
		return count;
	}
}
