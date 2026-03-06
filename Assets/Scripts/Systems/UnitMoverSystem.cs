using System;
using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct UnitMoverSystem : ISystem
    {
        public const float REACHED_TARGET_POSITION_DISTANCE_SQ = 2f;

        public ComponentLookup<TargetPositionPathQueued> TargetPositionPathQueuedComponentLookup;
        public ComponentLookup<FlowFieldPathRequest> FlowFieldPathRequestComponentLookup;
        public ComponentLookup<FlowFieldFollower> FlowFieldFollowerComponentLookup;
        public ComponentLookup<MoveOverride> MoveOverrideComponentLookup;
        public ComponentLookup<GridSystem.GridNode> GridNodeComponentLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<GridSystem.GridSystemData>();

            TargetPositionPathQueuedComponentLookup = SystemAPI.GetComponentLookup<TargetPositionPathQueued>(false);
            FlowFieldPathRequestComponentLookup = SystemAPI.GetComponentLookup<FlowFieldPathRequest>(false);
            FlowFieldFollowerComponentLookup = SystemAPI.GetComponentLookup<FlowFieldFollower>(false);
            MoveOverrideComponentLookup = SystemAPI.GetComponentLookup<MoveOverride>(false);
            GridNodeComponentLookup = SystemAPI.GetComponentLookup<GridSystem.GridNode>(false);
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridSystemData = SystemAPI.GetSingleton<GridSystem.GridSystemData>();

            TargetPositionPathQueuedComponentLookup.Update(ref state);
            FlowFieldPathRequestComponentLookup.Update(ref state);
            FlowFieldFollowerComponentLookup.Update(ref state);
            MoveOverrideComponentLookup.Update(ref state);
            GridNodeComponentLookup.Update(ref state);

            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;

            var targetPositionPathQueuedJob = new TargetPositionPathQueuedJob
            {
                CollisionWorld = collisionWorld,
                CostMap = gridSystemData.CostMap,
                Width = gridSystemData.Width,
                Height = gridSystemData.Height,
                GridNodeSize = gridSystemData.GridNodeSize,
                FlowFieldPathRequestComponentLookup = FlowFieldPathRequestComponentLookup,
                FlowFieldFollowerComponentLookup = FlowFieldFollowerComponentLookup,
                MoveOverrideComponentLookup = MoveOverrideComponentLookup,
                TargetPositionPathQueuedComponentLookup = TargetPositionPathQueuedComponentLookup
            };
            targetPositionPathQueuedJob.ScheduleParallel();
            
            var testCanMoveStraightJob = new TestCanMoveStraightJob
            {
                CollisionWorld = collisionWorld,
                FlowFieldFollowerComponentLookup = FlowFieldFollowerComponentLookup
            };
            testCanMoveStraightJob.ScheduleParallel();
            
            var flowFieldFollowerJob = new FlowFieldFollowerJob
            {
                Width = gridSystemData.Width,
                Height = gridSystemData.Height,
                GridNodeSize = gridSystemData.GridNodeSize,
                GridNodeSizeDouble = gridSystemData.GridNodeSize * 2f,
                FlowFieldFollowerComponentLookup = FlowFieldFollowerComponentLookup,
                TotalGridMapEntityArray = gridSystemData.TotalGridMapEntityArray,
                GridNodeComponentLookup = GridNodeComponentLookup,
            };
            flowFieldFollowerJob.ScheduleParallel();
            
            var unitMoverJob = new UnitMoverJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            };
            unitMoverJob.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct UnitMoverJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(
            ref LocalTransform localTransform,
            ref UnitMover unitMover,
            ref PhysicsVelocity physicsVelocity)
        {
            var moveDirection = unitMover.TargetPosition - localTransform.Position;

            var reachedTargetDistanceSq = UnitMoverSystem.REACHED_TARGET_POSITION_DISTANCE_SQ;
            if (math.lengthsq(moveDirection) <= reachedTargetDistanceSq)
            {
                physicsVelocity.Linear = float3.zero;
                physicsVelocity.Angular = float3.zero;
                unitMover.IsMoving = false;
                return;
            }

            unitMover.IsMoving = true;
            
            moveDirection = math.normalize(moveDirection);
            
            localTransform.Rotation = math.slerp(localTransform.Rotation,
                quaternion.LookRotation(moveDirection, math.up()),
                DeltaTime * unitMover.RotationSpeed);
            
            physicsVelocity.Linear = moveDirection * unitMover.MoveSpeed;
            physicsVelocity.Angular = float3.zero;
        }
    }

    [BurstCompile]
    [WithAll(typeof(TargetPositionPathQueued))]
    public partial struct TargetPositionPathQueuedJob : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<TargetPositionPathQueued> TargetPositionPathQueuedComponentLookup;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<FlowFieldPathRequest> FlowFieldPathRequestComponentLookup;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<FlowFieldFollower> FlowFieldFollowerComponentLookup;

        [NativeDisableParallelForRestriction] public ComponentLookup<MoveOverride> MoveOverrideComponentLookup;

        [ReadOnly] public CollisionWorld CollisionWorld;
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public NativeArray<byte> CostMap;
        [ReadOnly] public float GridNodeSize;

        public void Execute(
            in LocalTransform localTransform,
            ref UnitMover unitMover,
            Entity entity)
        {
            var raycastInput = new RaycastInput
            {
                Start = localTransform.Position,
                End = TargetPositionPathQueuedComponentLookup[entity].TargetPosition,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << GameAssets.PATHFINDING_WALLS,
                    GroupIndex = 0
                }
            };

            if (!CollisionWorld.CastRay(raycastInput))
            {
                unitMover.TargetPosition = TargetPositionPathQueuedComponentLookup[entity].TargetPosition;
                FlowFieldPathRequestComponentLookup.SetComponentEnabled(entity, false);
                FlowFieldFollowerComponentLookup.SetComponentEnabled(entity, false);
            }
            else
            {
                if (MoveOverrideComponentLookup.HasComponent(entity))
                {
                    MoveOverrideComponentLookup.SetComponentEnabled(entity, false);
                }

                if (GridSystem.IsValidWalkableGridPosition(
                        TargetPositionPathQueuedComponentLookup[entity].TargetPosition, Width, Height, CostMap,
                        GridNodeSize))
                {
                    var flowFieldPathRequest = FlowFieldPathRequestComponentLookup[entity];
                    flowFieldPathRequest.TargetPosition =
                        TargetPositionPathQueuedComponentLookup[entity].TargetPosition;
                    FlowFieldPathRequestComponentLookup[entity] = flowFieldPathRequest;
                    FlowFieldPathRequestComponentLookup.SetComponentEnabled(entity, true);
                }
                else
                {
                    unitMover.TargetPosition = localTransform.Position;
                    FlowFieldPathRequestComponentLookup.SetComponentEnabled(entity, false);
                    FlowFieldFollowerComponentLookup.SetComponentEnabled(entity, false);
                }
            }

            TargetPositionPathQueuedComponentLookup.SetComponentEnabled(entity, false);
        }
    }

    [BurstCompile]
    [WithAll(typeof(FlowFieldFollower))]
    public partial struct TestCanMoveStraightJob : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<FlowFieldFollower> FlowFieldFollowerComponentLookup;

        [ReadOnly] public CollisionWorld CollisionWorld;

        public void Execute(
            in LocalTransform LocalTransform,
            ref UnitMover unitMover,
            Entity entity
        )
        {
            var flowFieldFollower = FlowFieldFollowerComponentLookup[entity];

            var raycastInput = new RaycastInput
            {
                Start = LocalTransform.Position,
                End = flowFieldFollower.TargetPosition,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << GameAssets.PATHFINDING_WALLS,
                    GroupIndex = 0
                }
            };

            if (!CollisionWorld.CastRay(raycastInput))
            {
                unitMover.TargetPosition = flowFieldFollower.TargetPosition;
                FlowFieldFollowerComponentLookup.SetComponentEnabled(entity, false);
            }
        }
    }

    [BurstCompile]
    [WithAll(typeof(FlowFieldFollower))]
    public partial struct FlowFieldFollowerJob : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<FlowFieldFollower> FlowFieldFollowerComponentLookup;

        [ReadOnly] public ComponentLookup<GridSystem.GridNode> GridNodeComponentLookup;
        [ReadOnly] public float GridNodeSize;
        [ReadOnly] public float GridNodeSizeDouble;
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public NativeArray<Entity> TotalGridMapEntityArray;

        public void Execute(
            in LocalTransform localTransform,
            ref UnitMover unitMover,
            Entity entity
        )
        {
            var flowFieldFollower = FlowFieldFollowerComponentLookup[entity];

            var gridPosition = GridSystem.GetGridPosition(localTransform.Position, GridNodeSize);
            var index = GridSystem.CalculateIndex(gridPosition, Width);
            var totalCount = Width * Height;
            var gridNodeEntity = TotalGridMapEntityArray[totalCount * flowFieldFollower.GridIndex + index];
            var gridNode = GridNodeComponentLookup[gridNodeEntity];
            var gridNodeMoveVector = GridSystem.GetWorldMovementVector(gridNode.Vector);

            if (GridSystem.IsWall(gridNode))
            {
                gridNodeMoveVector = flowFieldFollower.LastMoveVector;
            }
            else
            {
                flowFieldFollower.LastMoveVector = gridNodeMoveVector;
            }

            unitMover.TargetPosition =
                GridSystem.GetWorldCenterPosition(gridPosition.x, gridPosition.y, GridNodeSize) +
                gridNodeMoveVector * GridNodeSizeDouble;

            if (math.distance(localTransform.Position, flowFieldFollower.TargetPosition) < GridNodeSize)
            {
                // Target destination
                unitMover.TargetPosition = localTransform.Position;
                FlowFieldFollowerComponentLookup.SetComponentEnabled(entity, false);
            }

            FlowFieldFollowerComponentLookup[entity] = flowFieldFollower;
        }
    }
}