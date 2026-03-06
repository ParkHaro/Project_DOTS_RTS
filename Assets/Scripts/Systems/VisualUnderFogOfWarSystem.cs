using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct VisualUnderFogOfWarSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameSceneTag>();
            
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;

            var entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

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
        }
    }
}