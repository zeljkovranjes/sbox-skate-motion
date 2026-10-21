namespace SkateMotion.Engine;

/// <summary>
/// Rebuilds a raw pose container from its packed arrangement (see dev/pack_poses.py): the headers came first, then
/// every track's keys as one stream of 32-bit words split into four byte planes, each track's words XORed with the
/// previous frame's. Walking the headers gives each track's key count, so the keys go back after their clip's header,
/// restored, and the result is the original file byte for byte.
/// </summary>
internal static class PackedPoseLayout
{
	internal static byte[] Restore( byte[] payload, int headerLength, int originalLength )
	{
		if ( headerLength < 0 || headerLength > payload.Length || (payload.Length - headerLength) % 4 != 0 )
			throw new FormatException( "Invalid packed pose layout." );
		var planes = payload.Length - headerLength;
		var count = planes / 4;
		var words = new uint[ count ];
		for ( var i = 0; i < count; i++ )
		{
			words[ i ] = payload[ headerLength + i ] | (uint)payload[ headerLength + count + i ] << 8
				| (uint)payload[ headerLength + count * 2 + i ] << 16 | (uint)payload[ headerLength + count * 3 + i ] << 24;
		}
		var restore = new Restorer( payload, headerLength, words, originalLength );
		restore.Container();
		return restore.Finish();
	}

	private sealed class Restorer
	{
		private readonly byte[] _headers;
		private readonly int _headerLength;
		private readonly uint[] _words;
		private readonly byte[] _output;
		private int _header;
		private int _word;
		private int _out;

		internal Restorer( byte[] headers, int headerLength, uint[] words, int originalLength )
		{
			_headers = headers;
			_headerLength = headerLength;
			_words = words;
			_output = new byte[ originalLength ];
		}

		internal byte[] Finish()
		{
			if ( _out != _output.Length || _header != _headerLength || _word != _words.Length )
				throw new FormatException( "The packed poses do not restore their original length." );
			return _output;
		}

		// Copies header bytes to the output as they are read.
		private int Copy( int length )
		{
			if ( length < 0 || _header + length > _headerLength || _out + length > _output.Length )
				throw new FormatException( "Truncated packed pose headers." );
			System.Array.Copy( _headers, _header, _output, _out, length );
			var at = _header;
			_header += length;
			_out += length;
			return at;
		}

		private byte U8() => _headers[ Copy( 1 ) ];

		private int U16()
		{
			var at = Copy( 2 );
			return _headers[ at ] | _headers[ at + 1 ] << 8;
		}

		private uint U32()
		{
			var at = Copy( 4 );
			return (uint)(_headers[ at ] | _headers[ at + 1 ] << 8 | _headers[ at + 2 ] << 16 | _headers[ at + 3 ] << 24);
		}

		internal void Container()
		{
			var magic = U32();
			var version = U32();
			if ( magic != 0x4e414d53 || version < 2 || version > 5 )
				throw new FormatException( "Packed poses hold no version 2 to 5 raw pose container." );
			var bones = U16();
			for ( var bone = 0; bone < bones; bone++ )
			{
				Copy( U8() );
				Copy( 2 );
			}
			var references = U16();
			Copy( references * bones * 48 );
			var clips = U32();
			for ( var clip = 0u; clip < clips; clip++ )
			{
				Copy( U8() );
				Copy( 8 );
				var frames = U16();
				Copy( 4 );
				if ( version >= 3 )
					Copy( 32 );
				if ( version >= 4 )
				{
					var streams = U8();
					for ( var stream = 0; stream < streams; stream++ )
					{
						Copy( 48 );
						Copy( checked( (int)U32() * 4 ) );
					}
				}
				var kinds = Copy( bones * 3 );
				for ( var track = 0; track < bones * 3; track++ )
				{
					var kind = _headers[ kinds + track ];
					Keys( kind == 0 ? 0 : kind == 1 ? 1 : frames );
				}
			}
		}

		// One track's keys, each word XORed back with the same lane of the frame before.
		private void Keys( int count )
		{
			var words = count * 4;
			if ( _word + words > _words.Length || _out + words * 4 > _output.Length )
				throw new FormatException( "Truncated packed pose keys." );
			for ( var key = 0; key < count; key++ )
			{
				for ( var lane = 0; lane < 4; lane++ )
				{
					var value = _words[ _word + key * 4 + lane ];
					if ( key > 0 )
						value ^= Read( _out - 16 );
					Write( value );
				}
			}
			_word += words;
		}

		private uint Read( int at ) =>
			(uint)(_output[ at ] | _output[ at + 1 ] << 8 | _output[ at + 2 ] << 16 | _output[ at + 3 ] << 24);

		private void Write( uint value )
		{
			_output[ _out ] = (byte)value;
			_output[ _out + 1 ] = (byte)(value >> 8);
			_output[ _out + 2 ] = (byte)(value >> 16);
			_output[ _out + 3 ] = (byte)(value >> 24);
			_out += 4;
		}
	}
}
