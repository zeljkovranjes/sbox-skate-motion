using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationEntryActionTests
{
	[Fact]
	public void LookupPreservesSixthWordWrappedHashAndMatchingSentinel()
	{
		var key = new InputCommandKey( uint.MaxValue, 2, 0, 0, 0, 3 );
		var different = key with { F = 5 };
		var entries = new AnimationActionValueEntry[]
		{
			new( different, 9f, 1 ), new( key, .75f, -1 ), new( key, 99f, 1 )
		};
		var table = new AnimationActionValueTable( [0, -1, 2], entries );
		Assert.True( table.TryGet( key, out var value ) );
		Assert.Equal( .75f, value );
		var sentinelFirst = new AnimationActionValueTable( [2, -1, 2], entries );
		Assert.False( sentinelFirst.TryGet( key, out _ ) );
	}

	[Fact]
	public void ActionsPreserveQueryKindFilteringFallbackModesAndEntryGate()
	{
		var context = new Context();
		var key = new InputCommandKey( 1, 2, 3, 4, 5, 6 );
		var attribute = new AnimationAttributeKey( 8, 0, 0, 0, 0 );
		AnimationEntryAction[] actions =
		[
			new( 0, key, attribute, default, 10f, 0xc0000000u ),
			new( 1, key, attribute, default, 20f, 0xc0000000u ),
			new( 2, key, attribute, default, 30f, 0xc0000000u )
		];
		AnimationEntryActionProcessor.Execute( actions, true, context );
		Assert.Equal( 1, context.Queries );
		Assert.Equal( new[] { (20f, true), (30f, false) }, context.Values );
		Assert.Equal( new AnimationAttributeKey( 1, 2, 3, 4, 5 ), context.LastName );
		context.Found = false;
		context.Values.Clear();
		AnimationEntryActionProcessor.Execute( actions, true, context );
		Assert.Equal( new[] { (10f, false), (20f, true), (30f, false) }, context.Values );
		context.Values.Clear();
		AnimationEntryActionProcessor.Execute( actions, false, context );
		Assert.Equal( 2, context.Queries );
		Assert.Equal( new[] { (20f, true), (30f, false) }, context.Values );
	}

	private sealed class Context : IAnimationEntryActionContext
	{
		private readonly AnimationActionValueTable _empty = new( [-1, -1], [] );
		internal readonly List<(float, bool)> Values = new();
		internal int Queries;
		internal bool Found = true;
		internal AnimationAttributeKey LastName;

		public AnimationAttributeKey EmptyTargetKey => default;

		public bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result )
		{
			Assert.Equal( new AnimationAttributeQueryResult( default, default, -1f, -1f, 6, 0 ), result );
			Queries++;
			result = result with { Kind = 1 };
			return Found;
		}

		public AnimationActionValueTable GetValueTable( uint kind ) => _empty;

		public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner )
		{
			Assert.Equal( -1, owner );
			LastName = name;
			Values.Add( (value, normalized) );
		}
	}
}
