using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace DotsRts
{
    public class VisualUnderFogOfWarAuthoring : MonoBehaviour
    {
        public GameObject ParentGameObject;
        public float SphereCastSize;

        private class VisualUnderFogOfWarAuthoringBaker : Baker<VisualUnderFogOfWarAuthoring>
        {
            public override void Bake(VisualUnderFogOfWarAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VisualUnderFogOfWar
                {
                    IsVisible = false,
                    ParentEntity = GetEntity(authoring.ParentGameObject, TransformUsageFlags.Dynamic),
                    SphereCastSize = authoring.SphereCastSize,
                    Timer = 0f,
                    TimerMax = 0.2f,
                });
                AddComponent(entity, new DisableRendering());
            }
        }
    }

    public struct VisualUnderFogOfWar : IComponentData
    {
        public bool IsVisible;
        public Entity ParentEntity;
        public float SphereCastSize;
        public float Timer;
        public float TimerMax;
    }
}