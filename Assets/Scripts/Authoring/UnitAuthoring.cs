using Unity.Entities;
using UnityEngine;

namespace DotsRts
{
    public class UnitAuthoring : MonoBehaviour
    {
        private class UnitAuthoringBaker : Baker<UnitAuthoring>
        {
            public override void Bake(UnitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Unit());
            }
        }
    }

    public struct Unit : IComponentData
    {
    }
}