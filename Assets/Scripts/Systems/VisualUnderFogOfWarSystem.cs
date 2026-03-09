using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct VisualUnderFogOfWarSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _localTransformComponentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameSceneTag>();

            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _localTransformComponentLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;

            var entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            _localTransformComponentLookup.Update(ref state);

            var visualUnderFogOfWarJob = new VisualUnderFogOfWarJob
            {
                CollisionWorld = collisionWorld,
                EntityCommandBuffer = entityCommandBuffer.AsParallelWriter(),
                LocalTransformComponentLookup = _localTransformComponentLookup,
                DeltaTime = SystemAPI.Time.DeltaTime,
            };
            visualUnderFogOfWarJob.ScheduleParallel();

            /*
            foreach (var (visualUnderFogOfWar,
                         entity)
                     in SystemAPI.Query<
                         RefRW<VisualUnderFogOfWar>>().WithEntityAccess())
            {
                var parentLocalTransform =
                    SystemAPI.GetComponent<LocalTransform>(visualUnderFogOfWar.ValueRO.ParentEntity);

                if (!collisionWorld.SphereCast(
                        parentLocalTransform.Position,
                        visualUnderFogOfWar.ValueRO.SphereCastSize,
                        new float3(0, 1, 0),
                        100,
                        new CollisionFilter
                        {
                            BelongsTo = ~0u,
                            CollidesWith = 1u << GameAssets.FOG_OF_WAR,
                            GroupIndex = 0,
                        }))
                {
                    // Not under visible fog of war, hide it
                    if (visualUnderFogOfWar.ValueRO.IsVisible)
                    {
                        visualUnderFogOfWar.ValueRW.IsVisible = false;
                        entityCommandBuffer.AddComponent<DisableRendering>(entity);
                    }
                }
                else
                {
                    // Under visible fog of war, show it
                    if (!visualUnderFogOfWar.ValueRO.IsVisible)
                    {
                        visualUnderFogOfWar.ValueRW.IsVisible = true;
                        entityCommandBuffer.RemoveComponent<DisableRendering>(entity);
                    }
                }
            }
        */
        }

        [BurstCompile]
        public partial struct VisualUnderFogOfWarJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformComponentLookup;
            [ReadOnly] public CollisionWorld CollisionWorld;

            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            public float DeltaTime;

            public void Execute(ref VisualUnderFogOfWar visualUnderFogOfWar, [ChunkIndexInQuery] int chunkIndexQuery,
                Entity entity)
            {
                visualUnderFogOfWar.Timer -= DeltaTime;
                if (visualUnderFogOfWar.Timer > 0f)
                {
                    return;
                }

                visualUnderFogOfWar.Timer += visualUnderFogOfWar.TimerMax;

                var parentLocalTransform = LocalTransformComponentLookup[visualUnderFogOfWar.ParentEntity];

                if (!CollisionWorld.SphereCast(
                        parentLocalTransform.Position,
                        visualUnderFogOfWar.SphereCastSize,
                        new float3(0, 1, 0),
                        100,
                        new CollisionFilter
                        {
                            BelongsTo = ~0u,
                            CollidesWith = 1u << GameAssets.FOG_OF_WAR,
                            GroupIndex = 0,
                        }))
                {
                    // Not under visible fog of war, hide it
                    if (visualUnderFogOfWar.IsVisible)
                    {
                        visualUnderFogOfWar.IsVisible = false;
                        EntityCommandBuffer.AddComponent<DisableRendering>(chunkIndexQuery, entity);
                    }
                }
                else
                {
                    // Under visible fog of war, show it
                    if (!visualUnderFogOfWar.IsVisible)
                    {
                        visualUnderFogOfWar.IsVisible = true;
                        EntityCommandBuffer.RemoveComponent<DisableRendering>(chunkIndexQuery, entity);
                    }
                }
            }
        }
    }
}