using Sandbox;
using SkateMotion.Core;

namespace SkateMotion.Engine;

internal sealed class BoardRig
{
	internal Model DeckModel { get; init; }
	internal Model WheelModel { get; init; }
	internal GameObject[] Wheels { get; }
	internal Vector3[] Origins { get; } = new Vector3[ 4 ];
	internal Rotation[] WheelRotations { get; } = new Rotation[ 4 ];
	internal bool HasCustomWheels { get; }

	internal BoardRig( GameObject board, SkateSettings settings, float radius, GameObject[] wheels = null )
	{
		Wheels = wheels ?? new GameObject[ 4 ];
		var count = 0;
		foreach ( var wheel in Wheels )
			if ( wheel.IsValid() ) count++;
		if ( count != 0 && count != 4 )
			throw new ArgumentException( "Assign all four wheel objects, or leave all four empty." );
		HasCustomWheels = count == 4;
		for ( var i = 0; i < 4; i++ )
		{
			Origins[ i ] = SkateUnits.Inches( SkateWheels.Offset( i, settings ) );
			WheelRotations[ i ] = Rotation.Identity;
			if ( !HasCustomWheels )
				continue;
			Origins[ i ] = board.WorldRotation.Inverse * (Wheels[ i ].WorldPosition - board.WorldPosition)
				+ Vector3.Up * ((settings.RideHeight - radius) * SkateUnits.InchesPerMetre);
			WheelRotations[ i ] = board.WorldRotation.Inverse * Wheels[ i ].WorldRotation;
		}
	}
}
