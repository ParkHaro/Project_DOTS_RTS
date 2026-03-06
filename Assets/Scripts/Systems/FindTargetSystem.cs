using System.Diagnostics;
using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct FindTargetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;
            var distanceHitList = new NativeList<DistanceHit>(Allocator.Temp);

            foreach (var (localTransform,
                         findTarget,
                         target,
                         targetOverride)
                     in SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<FindTarget>,
                         RefRW<Target>,
                         RefRO<TargetOverride>>())
            {
                findTarget.ValueRW.Timer -= SystemAPI.Time.DeltaTime;
                if (findTarget.ValueRO.Timer > 0f)
                {
                    continue;
                }

                findTarget.ValueRW.Timer = findTarget.ValueRO.TimerMax;
                if (targetOverride.ValueRO.TargetEntity != Entity.Null)
                {
                    target.ValueRW.TargetEntity = targetOverride.ValueRO.TargetEntity;
                    continue;
                }

                distanceHitList.Clear();
                var collisionFilter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << GameAssets.UNITS_LAYER | 1u << GameAssets.BUILDINGS_LAYER,
                    GroupIndex = 0,
                };

                var closestTargetEntity = Entity.Null;
                var closestTargetDistance = float.MaxValue;
                var currentTargetDistanceOffset = 0f;
                if (target.ValueRO.TargetEntity != Entity.Null)
                {
                    closestTargetEntity = target.ValueRO.TargetEntity;
                    var targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(target.ValueRO.TargetEntity);
                    closestTargetDistance =
                        math.distance(localTransform.ValueRO.Position, targetLocalTransform.Position);
                    currentTargetDistanceOffset = 2f;
                }

                if (collisionWorld.OverlapSphere(localTransform.ValueRO.Position,
                        findTarget.ValueRO.Range, ref distanceHitList, collisionFilter))
                {
                    foreach (var distanceHit in distanceHitList)
                    {
                        if (!SystemAPI.Exists(distanceHit.Entity) ||
                            !SystemAPI.HasComponent<Faction>(distanceHit.Entity))
                        {
                            continue;
                        }

                        var targetFaction = SystemAPI.GetComponent<Faction>(distanceHit.Entity);
                        if (targetFaction.FactionType == findTarget.ValueRO.TargetFaction)
                        {
                            if (closestTargetEntity == Entity.Null)
                            {
                                closestTargetEntity = distanceHit.Entity;
                                closestTargetDistance = distanceHit.Distance;
                            }
                            else
                            {
                                if (distanceHit.Distance + currentTargetDistanceOffset < closestTargetDistance)
                                {
                                    closestTargetEntity = distanceHit.Entity;
                                    closestTargetDistance = distanceHit.Distance;
                                }
                            }
                        }
                    }
                }

                if (closestTargetEntity != Entity.Null)
                {
                    target.ValueRW.TargetEntity = closestTargetEntity;
                }
            }
        }
    }
}