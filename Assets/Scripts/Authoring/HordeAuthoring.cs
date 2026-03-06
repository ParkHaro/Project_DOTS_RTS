using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace DotsRts
{
    public class HordeAuthoring : MonoBehaviour
    {
        public float StartTimer;
        public float SpawnTimerMax;
        public int ZombieAmountToSpawn;
        public float SpawnAreaWidth;
        public float SpawnAreaHeight;


        private class HordeAuthoringBaker : Baker<HordeAuthoring>
        {
            public override void Bake(HordeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Horde
                {
                    StartTimer = authoring.StartTimer,
                    SpawnTimerMax = authoring.SpawnTimerMax,
                    ZombieAmountToSpawn = authoring.ZombieAmountToSpawn,
                    SpawnAreaWidth = authoring.SpawnAreaWidth,
                    SpawnAreaHeight = authoring.SpawnAreaHeight,
                    Random = new Unity.Mathematics.Random((uint)entity.Index),
                });
            }
        }
    }

    public struct Horde : IComponentData
    {
        public float StartTimer;
        public float SpawnTimer;
        public float SpawnTimerMax;
        public int ZombieAmountToSpawn;
        public float SpawnAreaWidth;
        public float SpawnAreaHeight;
        public Unity.Mathematics.Random Random;
    }
}