using Unity.Entities;
using UnityEngine;

namespace DotsRts
{
    public class MainMenuSceneTagAuthoring : MonoBehaviour
    {
        private class MainMenuSceneTagAuthoringBaker : Baker<MainMenuSceneTagAuthoring>
        {
            public override void Bake(MainMenuSceneTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<MainMenuSceneTag>(entity);
            }
        }
    }

    public struct MainMenuSceneTag : IComponentData
    {
    }
}