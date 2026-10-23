namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Holds the feet a walking or running body stands on where they were put down: each planted foot is held in place across
/// the ground (its height stays the pose's). A pose that takes a held foot further than <see cref="Slip"/> from its hold
/// (the body setting off, turning on the spot, or settling into a stand with its feet somewhere else) has the foot step
/// over: lifted up to <see cref="StepLift"/> and put down where the pose has it over <see cref="StepTime"/>, rather than
/// slid there. One foot steps at a time; the other, taken past Slip meanwhile, has its hold dragged along, so it is never
/// shown further than Slip from the pose. A foot let go of (lifted by the pose) goes from where it was held back to the
/// pose over <see cref="Release"/> seconds. Positions are metres in any fixed frame (the world's), Z up.
/// </summary>
internal sealed class StanceLock
{
	/// <summary>Farthest (metres, across the ground) the pose takes a held foot from its hold before it steps over.</summary>
	internal const float Slip = 0.08f;

	/// <summary>Seconds a foot let go of takes to be back on the pose.</summary>
	internal const float Release = 0.1f;

	/// <summary>Seconds a step over takes.</summary>
	internal const float StepTime = 0.15f;

	/// <summary>Highest (metres) a foot is lifted over the pose's own height as it steps over.</summary>
	internal const float StepLift = 0.03f;

	private readonly bool[] _holding = new bool[ 2 ];
	private readonly Vector3[] _hold = new Vector3[ 2 ];
	private readonly float[] _letGoFor = new float[ 2 ];
	private readonly float[] _stepFor = new float[ 2 ];

	/// <summary>How many of the two feet are held.</summary>
	internal int Held => (_holding[ 0 ] ? 1 : 0) + (_holding[ 1 ] ? 1 : 0);

	/// <summary>Whether this foot (0 or 1) is held.</summary>
	internal bool Holding( int foot ) => _holding[ foot ];

	/// <summary>Whether this foot (0 or 1) is stepping over to where the pose has it.</summary>
	internal bool Stepping( int foot ) => _stepFor[ foot ] > 0f;

	/// <summary>
	/// Moves each of the two feet from where the pose puts them (<paramref name="feet"/>) to where they are shown, given which
	/// are planted.
	/// </summary>
	internal void Update( Span<Vector3> feet, ReadOnlySpan<bool> planted, float delta )
	{
		for ( var foot = 0; foot < 2; foot++ )
		{
			if ( _stepFor[ foot ] > 0f )
			{
				Step( foot, feet, planted[ foot ], delta );
				continue;
			}
			if ( planted[ foot ] )
			{
				Hold( foot, feet );
				continue;
			}
			if ( _holding[ foot ] )
			{
				_holding[ foot ] = false;
				_letGoFor[ foot ] = Release;
			}
			if ( _letGoFor[ foot ] <= 0f )
				continue;
			_letGoFor[ foot ] = MathF.Max( 0f, _letGoFor[ foot ] - delta );
			feet[ foot ] = Easing( foot, feet[ foot ] );
		}
	}

	/// <summary>Forgets every hold and step at once (the body placed somewhere else, or off its feet).</summary>
	internal void Reset()
	{
		for ( var foot = 0; foot < 2; foot++ )
		{
			_holding[ foot ] = false;
			_letGoFor[ foot ] = 0f;
			_stepFor[ foot ] = 0f;
		}
	}

	private void Hold( int foot, Span<Vector3> feet )
	{
		if ( !_holding[ foot ] )
		{
			// Held from where it is shown: still going back from a hold before, it goes on from there.
			_hold[ foot ] = Easing( foot, feet[ foot ] );
			_holding[ foot ] = true;
			_letGoFor[ foot ] = 0f;
		}
		var across = Across( _hold[ foot ], feet[ foot ] );
		var length = across.Length();
		if ( length > Slip )
		{
			if ( _stepFor[ 1 - foot ] <= 0f )
			{
				// It steps over from its hold, shown there this frame.
				_holding[ foot ] = false;
				_stepFor[ foot ] = StepTime;
				feet[ foot ] += across;
				return;
			}
			across *= Slip / length;
			_hold[ foot ] = feet[ foot ] + across;
		}
		feet[ foot ] += across;
	}

	// Stepping over: from its hold across to the pose, eased in and out, lifted in an arc over the pose's height.
	private void Step( int foot, Span<Vector3> feet, bool planted, float delta )
	{
		_stepFor[ foot ] = MathF.Max( 0f, _stepFor[ foot ] - delta );
		var done = 1f - _stepFor[ foot ] / StepTime;
		var share = done * done * (3f - 2f * done);
		var free = feet[ foot ];
		var shown = free + Across( _hold[ foot ], free ) * (1f - share);
		shown.Z = free.Z + StepLift * MathF.Sin( MathF.PI * done );
		feet[ foot ] = shown;
		// Put down where the pose has it: held there while the pose keeps it planted.
		if ( _stepFor[ foot ] > 0f )
			return;
		_hold[ foot ] = free;
		_holding[ foot ] = planted;
	}

	// Where a foot going back from a hold is shown: between where it was held and the pose, the hold's share falling with the
	// time left, and never further than Slip from the pose (the body may have been moved meanwhile).
	private Vector3 Easing( int foot, Vector3 free )
	{
		if ( _letGoFor[ foot ] <= 0f )
			return free;
		var offset = Across( _hold[ foot ], free ) * (_letGoFor[ foot ] / Release);
		var length = offset.Length();
		if ( length > Slip )
			offset *= Slip / length;
		return free + offset;
	}

	private static Vector3 Across( Vector3 to, Vector3 from ) => new( to.X - from.X, to.Y - from.Y, 0f );
}
