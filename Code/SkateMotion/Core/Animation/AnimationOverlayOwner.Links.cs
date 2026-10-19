using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal sealed partial class AnimationOverlayOwner
{
	private void Insert( Entry entry, int priority )
	{
		entry.Priority = priority;
		var current = _head;
		if ( current is null )
		{
			_head = entry;
			return;
		}
		while ( current.Next is not null && current.Priority > priority )
			current = current.Next;
		if ( current.Priority > priority )
		{
			entry.Next = current.Next;
			current.Next = entry;
			entry.Previous = current;
			if ( entry.Next is not null )
				entry.Next.Previous = entry;
			return;
		}
		entry.Next = current;
		entry.Previous = current.Previous;
		current.Previous = entry;
		if ( entry.Previous is null )
			_head = entry;
		else
			entry.Previous.Next = entry;
		// Creation retains its original base child; priority insertion only changes list links.
	}

	internal bool Remove( InputCommandKey channel )
	{
		var entry = Find( channel );
		if ( entry is null )
			return false;
		_freeEntries[ _freeEntryCount++ ] = entry.Index;
		entry.Owner.ReleaseTrees();
		Erase( entry );
		var previous = entry.Previous;
		var next = entry.Next;
		if ( previous is null )
		{
			_head = next;
			if ( next is not null )
				next.Previous = null;
		}
		else
		{
			previous.Next = next;
			previous.Base.Target = next is null ? BaseRoot : next.Node;
			if ( next is not null )
				next.Previous = previous;
		}
		entry.Priority = 0;
		entry.Previous = null;
		entry.Next = null;
		entry.HashNext = null;
		return true;
	}

	private void Erase( Entry entry )
	{
		var bucket = Bucket( entry.Key );
		Entry previous = null;
		var current = _buckets[ bucket ];
		while ( !ReferenceEquals( current, entry ) )
		{
			previous = current;
			current = current.HashNext;
		}
		if ( previous is null )
			_buckets[ bucket ] = entry.HashNext;
		else
			previous.HashNext = entry.HashNext;
		_count--;
	}
}
