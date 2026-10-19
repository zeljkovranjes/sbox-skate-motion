namespace SkateMotion.Core.Animation;

/// <summary>Exact fixed-width attribute identity, including otherwise unknown trailing words.</summary>
internal readonly record struct AnimationAttributeKey( uint A, uint B, uint C, uint D, uint E )
{
	internal static AnimationAttributeKey Encode( string name )
	{
		ArgumentNullException.ThrowIfNull( name );
		return new( Word( name, 0 ), Word( name, 6 ), Word( name, 12 ), Word( name, 18 ), Word( name, 24 ) );
	}

	private static uint Word( string name, int start )
	{
		uint result = 0;
		for ( var i = start; i < start + 6; i++ )
		{
			var digit = 0;
			if ( i < name.Length )
			{
				var character = name[ i ];
				digit = character - (character > 96 ? 86 : character <= 57 ? 47 : character <= 90 ? 54 : 58);
			}
			result = unchecked( result * 38 + (uint)digit );
		}
		return result;
	}
}
