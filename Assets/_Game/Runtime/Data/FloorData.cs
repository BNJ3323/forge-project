using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [CreateAssetMenu(menuName = "Forge/Floor", fileName = "Floor_1")]
    public class FloorData : ScriptableObject
    {
        [Range(1, 3)] public int floorNumber = 1;
        public RoomData entrance;
        public List<RoomData> rooms = new List<RoomData>();
    }
}
