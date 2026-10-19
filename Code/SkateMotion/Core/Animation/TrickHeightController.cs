using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Selects entry-time trick height from configured, requested and sampled values.</summary>
internal static class TrickHeightController
{
	private static readonly InputCommandKey GestureSpeed = InputCommandKey.Encode( "GESTURESPEED"u8 );
	private static readonly InputCommandKey TrickHeight = InputCommandKey.Encode( "TRICKHEIGHT"u8 );

	internal static TrickHeightResult Evaluate( InputRequestBuffer requests,
		AnimationAttributeQueryResult[] attributes, TrickHeightSettings settings )
	{
		ArgumentNullException.ThrowIfNull( requests );
		ArgumentNullException.ThrowIfNull( attributes );
		TryRequest( requests, GestureSpeed, out var speed );
		float height;
		if ( settings.FixedHeight.HasValue )
			height = settings.FixedHeight.Value;
		else if ( !TryRequest( requests, TrickHeight, out height ) )
		{
			height = AttributeValue( attributes, settings.SourceAttribute );
			if ( settings.UseGestureSpeed )
			{
				if ( settings.ClampToAttribute )
				{
					// Subtract/select order preserves signed-zero and unordered comparisons.
					var nonnegativeSpeed = -speed >= 0f ? 0f : speed;
					height = height - nonnegativeSpeed >= 0f ? nonnegativeSpeed : height;
				}
				else
					height = speed;
			}
		}
		var jump = settings.FromManual ? speed * 0.27f : settings.FromGrind ? speed : speed * 0.2f;
		return new( height, jump );
	}

	private static float AttributeValue( AnimationAttributeQueryResult[] attributes, AnimationAttributeKey name )
	{
		foreach ( var attribute in attributes )
		{
			if ( attribute.Name == name )
				return attribute.Kind is >= 0 and <= 3 ? attribute.Payload.Scalar : 0f;
		}
		return 0f;
	}

	private static bool TryRequest( InputRequestBuffer requests, InputCommandKey name, out float value )
	{
		for ( var index = 0; index < requests.Count; index++ )
		{
			if ( requests[ index ].Key != name )
				continue;
			value = requests[ index ].Value;
			return true;
		}
		value = 0f;
		return false;
	}
}
