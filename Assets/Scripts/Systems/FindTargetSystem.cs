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
        private ComponentLookup<LocalTransform> _localTransformComponentLookup;
        private ComponentLookup<Faction> _factionComponentLookup;
        private EntityStorageInfoLookup _entityStorageInfoLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _localTransformComponentLookup = state.GetComponentLookup<LocalTransform>(true);
            _factionComponentLookup = state.GetComponentLookup<Faction>(true);
            _entityStorageInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;
            _localTransformComponentLookup.Update(ref state);
            _factionComponentLookup.Update(ref state);
            _entityStorageInfoLookup.Update(ref state);

            var findTargetJob = new FindTargetJob
            {
                LocalTransformComponentLookup = _localTransformComponentLookup,
                FactionComponentLookup = _factionComponentLookup,
                EntityStorageInfoLookup = _entityStorageInfoLookup,
                CollisionWorld = collisionWorld,
                DeltaTime = SystemAPI.Time.DeltaTime,
            };
            findTargetJob.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct FindTargetJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformComponentLookup;
        [ReadOnly] public ComponentLookup<Faction> FactionComponentLookup;
        [ReadOnly] public EntityStorageInfoLookup EntityStorageInfoLookup;
        [ReadOnly] public CollisionWorld CollisionWorld;
        public float DeltaTime;

        public void Execute(
            in LocalTransform localTransform,
            ref FindTarget findTarget,
            ref Target target,
            in TargetOverride targetOverride)
        {
            findTarget.Timer -= DeltaTime;
            if (findTarget.Timer > 0f)
            {
                return;
            }

            findTarget.Timer += findTarget.TimerMax;
            if (targetOverride.TargetEntity != Entity.Null)
            {
                target.TargetEntity = targetOverride.TargetEntity;
                return;
            }

            var distanceHitList = new NativeList<DistanceHit>(Allocator.TempJob);
            var collisionFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << GameAssets.UNITS_LAYER | 1u << GameAssets.BUILDINGS_LAYER,
                GroupIndex = 0,
            };

            var closestTargetEntity = Entity.Null;
            var closestTargetDistance = float.MaxValue;
            var currentTargetDistanceOffset = 0f;
            if (target.TargetEntity != Entity.Null)
            {
                closestTargetEntity = target.TargetEntity;
                var targetLocalTransform = LocalTransformComponentLookup[target.TargetEntity];
                closestTargetDistance =
                    math.distance(localTransform.Position, targetLocalTransform.Position);
                currentTargetDistanceOffset = 2f;
            }

            if (CollisionWorld.OverlapSphere(localTransform.Position,
                    findTarget.Range, ref distanceHitList, collisionFilter))
            {
                foreach (var distanceHit in distanceHitList)
                {
                    if (!EntityStorageInfoLookup.Exists(distanceHit.Entity) ||
                        !FactionComponentLookup.HasComponent(distanceHit.Entity))
                    {
                        continue;
                    }

                    var targetFaction = FactionComponentLookup[distanceHit.Entity];
                    if (targetFaction.FactionType == findTarget.TargetFaction)
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
                target.TargetEntity = closestTargetEntity;
            }

            distanceHitList.Dispose();
        }
    }
}