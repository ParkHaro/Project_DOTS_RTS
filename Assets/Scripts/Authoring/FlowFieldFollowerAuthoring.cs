using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsRts
{
    public class FlowFieldFollowerAuthoring : MonoBehaviour
    {
        private class FlowFieldFollowerAuthoringBaker : Baker<FlowFieldFollowerAuthoring>
        {
            public override void Bake(FlowFieldFollowerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<FlowFieldFollower>(entity, new FlowFieldFollower
                {
                    LastMoveVector = new float3(0, 0, 1)
                });
                SetComponentEnabled<FlowFieldFollower>(entity, false);
            }
        }
    }

    public struct FlowFieldFollower : IComponentData, IEnableableComponent
    {
        public float3 TargetPosition;
        public float3 LastMoveVector;
        public int GridIndex;
    }
}