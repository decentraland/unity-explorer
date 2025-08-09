using UnityEngine;

namespace DCL.Character.Components
{
	public struct TransformDirtyFlagComponent
	{
		private const float MINIMAL_DISTANCE_DIFFERENCE = 0.01f;
		
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
				float distance = CheapDistance(oldPosition, newPosition);
				
				if(distance >= MINIMAL_DISTANCE_DIFFERENCE)
					IsDirty = true;
			} 
			
			oldPosition = newPosition;
		}

		public void ClearDirty()
		{
			IsDirty = false;
		}

		private float CheapDistance(Vector3 positionA, Vector3 positionB)
		{
			Vector3 diff = positionA - positionB;
			return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.z);
		}
	}
}