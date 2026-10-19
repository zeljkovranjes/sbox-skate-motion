using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPlayBehaviorInstanceTests
{
	[Fact]
	public void CreationAndReentrySuppressOneUpdateWhileExitPreservesState()
	{
		var context = new Context();
		var instance = new AnimationPlayBehaviorInstance();
		AnimationEntryAction[] actions =
		[
			new( 0, default, default, default, 9f, 0x80000000u ),
			new( 2, new InputCommandKey( 1, 0, 0, 0, 0, 0 ), default, default, 2f, 0x80000000u )
		];
		Assert.True( instance.SuppressNextUpdate );
		instance.Update( actions, context );
		Assert.False( instance.SuppressNextUpdate );
		Assert.Equal( 0, context.Writes );
		instance.Update( actions, context );
		Assert.Equal( 1, context.Writes );
		instance.Enter();
		instance.Exit();
		Assert.True( instance.SuppressNextUpdate );
		instance.Update( actions, context );
		Assert.Equal( 1, context.Writes );
		instance.Exit();
		Assert.False( instance.SuppressNextUpdate );
		instance.Update( actions, context );
		Assert.Equal( 2, context.Writes );
	}

	private sealed class Context : IAnimationEntryActionContext
	{
		private readonly AnimationActionValueTable _table = new( [-1, -1], [] );
		internal int Writes;

		public AnimationAttributeKey EmptyTargetKey => default;

		public bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result ) =>
			throw new InvalidOperationException( "Scheduled updates must not query entry-only actions." );

		public AnimationActionValueTable GetValueTable( uint kind ) => _table;

		public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner )
		{
			Assert.Equal( 2f, value );
			Assert.False( normalized );
			Assert.Equal( -1, owner );
			Writes++;
		}
	}
}
