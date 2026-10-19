namespace SkateMotion.Core.Collision;

/// <summary>Retains request data separately from its contact and completion state.</summary>
internal sealed class TrajectoryQueryTarget
{
	internal TrajectoryQueryRequest Request { get; private set; }
	internal TrajectoryContact Contact { get; private set; } = TrajectoryContact.Empty;
	internal byte Flag { get; private set; }
	internal bool Completed { get; private set; }
	internal uint RequestRevision { get; private set; }
	internal uint ContactRevision { get; private set; }

	internal void Reset( TrajectoryQueryRequest request, byte flag )
	{
		Request = request;
		Contact = TrajectoryContact.Empty;
		Flag = flag;
		Completed = false;
		RequestRevision = unchecked( RequestRevision + 1 );
	}

	internal void Deliver( TrajectoryContact contact )
	{
		if ( contact.Time >= 0f )
		{
			Contact = contact;
			ContactRevision = unchecked( ContactRevision + 1 );
		}
		Completed = true;
	}
}
