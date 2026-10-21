using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardFitEdgeTests
{
	private static readonly BoardFit Clips = BoardFit.Default;

	// A deck as wide as the clips' that tapers to half its width toward its tail (negative along it), straight across.
	private static float? Tapered( float along, float across ) =>
		along < -0.2f ? Clips.Width * 0.5f * (1f - 0.5f * (-0.2f - along) / 0.25f) : Clips.Width * 0.5f;

	[Fact]
	public void AFootOverATaperedTailLandsWithinItsEdgesThere()
	{
		var point = new Vector3( -0.45f, -Clips.Width * 0.4f, 0f );
		var foot = Clips.Foot( point, Clips, Tapered );
		var here = Tapered( foot.X, point.Y ).Value;
		Assert.True( here < Clips.Width * 0.5f );
		// The same share of the way to its edge there as the clips' foot was to theirs.
		Assert.Equal( -here * 0.8f, foot.Y, 4 );
		Assert.Equal( Clips.Foot( point, Clips ).X, foot.X, 5 );
	}

	[Fact]
	public void AFootWhereTheDeckIsAsWideAsTheStanceIsUnchanged()
	{
		var point = new Vector3( 0.1f, Clips.Width * 0.45f, 0.01f );
		Assert.Equal( Clips.Foot( point, Clips ), Clips.Foot( point, Clips, Tapered ) );
		Assert.Equal( Clips.Foot( point, Clips ), Clips.Foot( point, Clips, ( _, _ ) => null ) );
	}

	[Fact]
	public void AFootBesideATaperedTailStepsOutPastItsEdgeThere()
	{
		var past = 0.05f;
		var point = new Vector3( -0.45f, Clips.Width * 0.5f + past, 0f );
		var beside = Clips.Beside( point, Clips, 0.05f, Tapered );
		Assert.Equal( Tapered( beside.X, point.Y ).Value + past, beside.Y, 4 );
	}
}
