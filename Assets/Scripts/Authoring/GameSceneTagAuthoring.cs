using Unity.Entities;
using UnityEngine;

namespace DotsRts
{
    public class GameSceneTagAuthoring : MonoBehaviour
    {
        private class GameSceneTagAuthoringBaker : Baker<GameSceneTagAuthoring>
        {
            public override void Bake(GameSceneTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<GameSceneTag>(entity);
            }
        }
    }

    public struct GameSceneTag : IComponentData
    {
    }
}