using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Explicit channel, request and output identities for a retained gesture behavior.</summary>
internal sealed class AnimationCharacterGestureDefinition
{
	private readonly InputCommandKey[] _channels;
	private readonly InputCommandKey[] _starts;
	private readonly InputCommandKey[] _held;
	private readonly InputCommandKey[] _suppressedChannels;
	internal InputCommandKey Mount { get; }
	internal InputCommandKey Dismount { get; }
	internal InputCommandKey Brake { get; }
	internal AnimationAttributeKey Distance { get; }

	internal AnimationCharacterGestureDefinition( InputCommandKey[] channels,
		InputCommandKey[] starts, InputCommandKey[] held, InputCommandKey[] suppressedChannels,
		InputCommandKey mount, InputCommandKey dismount, InputCommandKey brake,
		AnimationAttributeKey distance )
	{
		_channels = Copy( channels, 3 );
		_starts = Copy( starts, 4 );
		_held = Copy( held, 4 );
		_suppressedChannels = Copy( suppressedChannels, 3 );
		Mount = mount;
		Dismount = dismount;
		Brake = brake;
		Distance = distance;
	}

	internal InputCommandKey Channel( int mode ) => _channels[ mode ];
	internal InputCommandKey Start( int gesture ) => _starts[ gesture ];
	internal InputCommandKey Held( int gesture ) => _held[ gesture ];
	internal InputCommandKey SuppressedChannel( int index ) => _suppressedChannels[ index ];

	private static InputCommandKey[] Copy( InputCommandKey[] values, int count )
	{
		ArgumentNullException.ThrowIfNull( values );
		if ( values.Length != count )
			throw new ArgumentException( "The identity array has the wrong number of entries.", nameof( values ) );
		return values.AsSpan().ToArray();
	}
}
