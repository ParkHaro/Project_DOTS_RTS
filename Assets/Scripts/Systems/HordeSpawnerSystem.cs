using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace DotsRts.Systems
{
    public partial struct HordeSpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EntitiesReferences>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
            var entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (localTransform,
                         horde)
                     in SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<Horde>>())
            {
                horde.ValueRW.StartTimer -= SystemAPI.Time.DeltaTime;

                if (horde.ValueRO.StartTimer > 0)
                {
                    continue;
                }

                if (horde.ValueRO.ZombieAmountToSpawn <= 0)
                {
                    continue;
                }

                horde.ValueRW.SpawnTimer -= SystemAPI.Time.DeltaTime;
                if (horde.ValueRO.SpawnTimer <= 0)
                {
                    horde.ValueRW.SpawnTimer = horde.ValueRW.SpawnTimerMax;

                    var zombieEntity = entityCommandBuffer.Instantiate(entitiesReferences.ZombiePrefabEntity);

                    var random = horde.ValueRO.Random;
                    var spawnPosition = localTransform.ValueRO.Position;
                    spawnPosition.x += random.NextFloat(-horde.ValueRO.SpawnAreaWidth, +horde.ValueRO.SpawnAreaWidth);
                    spawnPosition.z += random.NextFloat(-horde.ValueRO.SpawnAreaHeight, +horde.ValueRO.SpawnAreaHeight);
                    horde.ValueRW.Random = random;

                    entityCommandBuffer.SetComponent(zombieEntity, LocalTransform.FromPosition(spawnPosition));
                    entityCommandBuffer.AddComponent<EnemyAttackHQ>(zombieEntity);

                    horde.ValueRW.ZombieAmountToSpawn--;
                }
            }
        }
    }
}