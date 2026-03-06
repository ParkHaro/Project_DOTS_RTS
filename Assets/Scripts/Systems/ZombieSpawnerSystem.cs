using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace DotsRts.Systems
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ZombieSpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<EntitiesReferences>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();

            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;
            var distanceHitList = new NativeList<DistanceHit>(Allocator.Temp);

            var entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (localTransform,
                         zombieSpawner)
                     in SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<ZombieSpawner>>())
            {
                zombieSpawner.ValueRW.Timer -= SystemAPI.Time.DeltaTime;
                if (zombieSpawner.ValueRO.Timer > 0f)
                {
                    continue;
                }

                zombieSpawner.ValueRW.Timer = zombieSpawner.ValueRO.TimerMax;

                distanceHitList.Clear();
                var collisionFilter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << GameAssets.UNITS_LAYER,
                    GroupIndex = 0,
                };

                var nearbyZombieAmount = 0;
                if (collisionWorld.OverlapSphere(localTransform.ValueRO.Position,
                        zombieSpawner.ValueRO.NearbyZombieAmountDistance, ref distanceHitList, collisionFilter))
                {
                    foreach (var distanceHit in distanceHitList)
                    {
                        if (!SystemAPI.Exists(distanceHit.Entity))
                        {
                            continue;
                        }

                        if (SystemAPI.HasComponent<Unit>(distanceHit.Entity) &&
                            SystemAPI.HasComponent<Zombie>(distanceHit.Entity))
                        {
                            nearbyZombieAmount++;
                        }
                    }
                }

                if (nearbyZombieAmount >= zombieSpawner.ValueRO.NearbyZombieAmountMax)
                {
                    continue;
                }

                var zombieEntity = state.EntityManager.Instantiate(entitiesReferences.ZombiePrefabEntity);
                SystemAPI.SetComponent(zombieEntity, LocalTransform.FromPosition(localTransform.ValueRO.Position));

                entityCommandBuffer.AddComponent(zombieEntity, new RandomWalking
                {
                    OriginPosition = localTransform.ValueRO.Position,
                    TargetPosition = localTransform.ValueRO.Position,
                    DistanceMin = zombieSpawner.ValueRO.RandomWalkingDistanceMin,
                    DistanceMax = zombieSpawner.ValueRO.RandomWalkingDistanceMax,
                    Random = new Random((uint)zombieEntity.Index),
                });
            }
        }
    }
}