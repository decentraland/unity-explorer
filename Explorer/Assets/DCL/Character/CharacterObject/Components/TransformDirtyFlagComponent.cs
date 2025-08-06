using UnityEngine;

namespace DCL.Character.Components
{
	public struct TransformDirtyFlagComponent
	{
		private const float MINIMAL_DISTANCE_DIFFERENCE = 0.2f;
		
		public bool IsDirty { get; private set; }

		private Vector3 oldPosition;

		public TransformDirtyFlagComponent(Vector3 position)
		{
			oldPosition = position;
			IsDirty = false;
		}

		public void PushNewPosition(Vector3 newPosition)
		{
			if (!IsDirty)
			{
				float distance = Vector3.Distance(oldPosition, newPosition);
				
				if(distance <= MINIMAL_DISTANCE_DIFFERENCE)
					IsDirty = true;
			} 
			
			oldPosition = newPosition;
		}

		public void ClearDirty()
		{
			IsDirty = false;
		}
	}
}