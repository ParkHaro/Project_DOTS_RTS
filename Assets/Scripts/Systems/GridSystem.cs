// #define GRID_DEBUG

using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace DotsRts.Systems
{
    public partial struct GridSystem : ISystem
    {
        public const int WALL_COST = byte.MaxValue;
        public const int HEAVY_COST = 50;
        public const int FLOW_FIELD_MAP_COUNT = 50;

        public struct GridSystemData : IComponentData
        {
            public int Width;
            public int Height;
            public float GridNodeSize;
            public NativeArray<GridMap> GridMapArray;
            public int NextGridIndex;
            public NativeArray<byte> CostMap;
            public NativeArray<Entity> TotalGridMapEntityArray;
        }

        public struct GridMap
        {
            public NativeArray<Entity> GridEntityArray;
            public int2 TargetGridPosition;
            public bool IsValid;
        }

        public struct GridNode : IComponentData
        {
            public int GridIndex;
            public int Index;
            public int X;
            public int Y;
            public byte Cost;
            public int BestCost;
            public float2 Vector;
        }

        public ComponentLookup<GridNode> gridNodeComponentLookup;

#if !GRID_DEBUG
        [BurstCompile]
#endif
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();

            var width = 20;
            var height = 10;
            var gridNodeSize = 5f;
            var totalCount = width * height;

            var gridNodeEntityPrefab = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<GridNode>(gridNodeEntityPrefab);

            var gridMapArray = new NativeArray<GridMap>(FLOW_FIELD_MAP_COUNT, Allocator.Persistent);
            var totalGridMapEntityList = new NativeList<Entity>(totalCount * FLOW_FIELD_MAP_COUNT, Allocator.Temp);
            for (int i = 0; i < FLOW_FIELD_MAP_COUNT; i++)
            {
                var gridMap = new GridMap();
                gridMap.IsValid = false;
                gridMap.GridEntityArray = new NativeArray<Entity>(totalCount, Allocator.Persistent);

                state.EntityManager.Instantiate(gridNodeEntityPrefab, gridMap.GridEntityArray);
                totalGridMapEntityList.AddRange(gridMap.GridEntityArray);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var index = CalculateIndex(x, y, width);
                        var gridNode = new GridNode
                        {
                            GridIndex = i,
                            Index = index,
                            X = x,
                            Y = y,
                        };
#if GRID_DEBUG
                        state.EntityManager.SetName(gridMap.GridEntityArray[index], $"GridNode_{x}_{y}");
#endif
                        SystemAPI.SetComponent(gridMap.GridEntityArray[index], gridNode);
                    }
                }

                gridMapArray[i] = gridMap;
            }

            state.EntityManager.AddComponent<GridSystemData>(state.SystemHandle);
            state.EntityManager.SetComponentData(state.SystemHandle,
                new GridSystemData
                {
                    Width = width,
                    Height = height,
                    GridNodeSize = gridNodeSize,
                    GridMapArray = gridMapArray,
                    CostMap = new NativeArray<byte>(totalCount, Allocator.Persistent),
                    TotalGridMapEntityArray = totalGridMapEntityList.ToArray(Allocator.Persistent)
                }
            );

            totalGridMapEntityList.Dispose();
            gridNodeComponentLookup = SystemAPI.GetComponentLookup<GridNode>(false);
        }

#if !GRID_DEBUG
        [BurstCompile]
#endif
        public void OnUpdate(ref SystemState state)
        {
            var gridSystemData = SystemAPI.GetComponent<GridSystemData>(state.SystemHandle);

            gridNodeComponentLookup.Update(ref state);

            foreach (var (flowFieldPathRequest,
                         FlowFieldPathRequestEnabled,
                         flowFieldFollower,
                         FlowFieldFollowerEnabled)
                     in SystemAPI.Query<
                         RefRW<FlowFieldPathRequest>,
                         EnabledRefRW<FlowFieldPathRequest>,
                         RefRW<FlowFieldFollower>,
                         EnabledRefRW<FlowFieldFollower>>().WithPresent<FlowFieldFollower>())
            {
                var targetGridPosition = GetGridPosition(flowFieldPathRequest.ValueRO.TargetPosition,
                    gridSystemData.GridNodeSize);

                FlowFieldPathRequestEnabled.ValueRW = false;

                var alreadyCalculatedPath = false;
                for (int i = 0; i < FLOW_FIELD_MAP_COUNT; i++)
                {
                    if (gridSystemData.GridMapArray[i].IsValid &&
                        gridSystemData.GridMapArray[i].TargetGridPosition.Equals(targetGridPosition))
                    {
                        flowFieldFollower.ValueRW.GridIndex = i;
                        flowFieldFollower.ValueRW.TargetPosition = flowFieldPathRequest.ValueRO.TargetPosition;
                        FlowFieldFollowerEnabled.ValueRW = true;

                        alreadyCalculatedPath = true;
                        break;
                    }
                }

                if (alreadyCalculatedPath)
                {
                    continue;
                }

                var gridIndex = gridSystemData.NextGridIndex;
                gridSystemData.NextGridIndex = (gridSystemData.NextGridIndex + 1) % FLOW_FIELD_MAP_COUNT;

                // Debug.Log($"Calculating Path to {targetGridPosition} :: {gridIndex}");
                flowFieldFollower.ValueRW.GridIndex = gridIndex;
                flowFieldFollower.ValueRW.TargetPosition = flowFieldPathRequest.ValueRO.TargetPosition;
                FlowFieldFollowerEnabled.ValueRW = true;

                var gridNodeNativeArray = new NativeArray<RefRW<GridNode>>(
                    gridSystemData.Width * gridSystemData.Height, Allocator.Temp);

                var initializeGridJob = new InitializeGridJob
                {
                    GridIndex = gridIndex,
                    TargetGridPosition = targetGridPosition,
                };
                var initializeGridJobHandle = initializeGridJob.ScheduleParallel(state.Dependency);
                initializeGridJobHandle.Complete();

                for (int x = 0; x < gridSystemData.Width; x++)
                {
                    for (int y = 0; y < gridSystemData.Height; y++)
                    {
                        var index = CalculateIndex(x, y, gridSystemData.Width);
                        var gridNodeEntity = gridSystemData.GridMapArray[gridIndex].GridEntityArray[index];
                        var gridNode = SystemAPI.GetComponentRW<GridNode>(gridNodeEntity);

                        gridNodeNativeArray[index] = gridNode;
                    }
                }

                var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                var collisionWorld = physicsWorldSingleton.CollisionWorld;

                var updateCostMapJob = new UpdateCostMapJob
                {
                    Width = gridSystemData.Width,
                    CollisionWorld = collisionWorld,
                    CostMap = gridSystemData.CostMap,
                    GridMap = gridSystemData.GridMapArray[gridIndex],
                    GridNodeSize = gridSystemData.GridNodeSize,
                    GridNodeSizeHalf = gridSystemData.GridNodeSize * 0.5f,
                    gridNodeComponentLookup = gridNodeComponentLookup,
                    CollisionFilterWall = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = 1u << GameAssets.PATHFINDING_WALLS,
                        GroupIndex = 0,
                    },
                    CollisionFilterHeavy = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = 1u << GameAssets.PATHFINDING_HEAVY,
                        GroupIndex = 0,
                    }
                };
                var updateCostMapJobHandle = updateCostMapJob.ScheduleParallel(
                    gridSystemData.Width * gridSystemData.Height,
                    50, state.Dependency);
                updateCostMapJobHandle.Complete();

                var gridNodeOpenQueue = new NativeQueue<RefRW<GridNode>>(Allocator.Temp);
                var targetGridNode = gridNodeNativeArray[CalculateIndex(targetGridPosition, gridSystemData.Width)];
                gridNodeOpenQueue.Enqueue(targetGridNode);

                var safety = 1000;
                while (gridNodeOpenQueue.Count > 0)
                {
                    safety--;
                    if (safety < 0)
                    {
                        // Debug.LogError("Safety break");
                        break;
                    }

                    var currentGridNode = gridNodeOpenQueue.Dequeue();
                    var neighborGridNodeList = GetNeighborGridNodeList(currentGridNode, gridNodeNativeArray,
                        gridSystemData.Width, gridSystemData.Height);

                    foreach (var neighborGridNode in neighborGridNodeList)
                    {
                        if (neighborGridNode.ValueRO.Cost == WALL_COST)
                        {
                            // This is a wall
                            continue;
                        }

                        var newBestCost = currentGridNode.ValueRO.BestCost + neighborGridNode.ValueRO.Cost;
                        if (newBestCost < neighborGridNode.ValueRO.BestCost)
                        {
                            neighborGridNode.ValueRW.BestCost = newBestCost;
                            neighborGridNode.ValueRW.Vector = CalculateVector(
                                neighborGridNode.ValueRO.X, neighborGridNode.ValueRO.Y,
                                currentGridNode.ValueRO.X, currentGridNode.ValueRO.Y);

                            gridNodeOpenQueue.Enqueue(neighborGridNode);
                        }
                    }

                    neighborGridNodeList.Dispose();
                }

                gridNodeOpenQueue.Dispose();
                gridNodeNativeArray.Dispose();

                var gridMap = gridSystemData.GridMapArray[gridIndex];
                gridMap.TargetGridPosition = targetGridPosition;
                gridMap.IsValid = true;
                gridSystemData.GridMapArray[gridIndex] = gridMap;
                SystemAPI.SetComponent(state.SystemHandle, gridSystemData);
            }

            /*
            if (Input.GetMouseButtonDown(0))
            {
                float3 mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();
                var mouseGridPosition = GetGridPosition(mouseWorldPosition, gridSystemData.GridNodeSize);
                if (IsValidGridPosition(mouseGridPosition, gridSystemData.Width, gridSystemData.Height))
                {
                    /*
                    var index = CalculateIndex(mouseGridPosition.x, mouseGridPosition.y, gridSystemData.Width);
                    var gridNodeEntity = gridSystemData.GridMap.GridEntityArray[index];
                    var gridNode = SystemAPI.GetComponentRW<GridNode>(gridNodeEntity);

                    foreach (var (flowFieldFollower,
                                 FlowFieldFollowerEnabled)
                             in SystemAPI.Query<
                                 RefRW<FlowFieldFollower>,
                                 EnabledRefRW<FlowFieldFollower>>().WithPresent<FlowFieldFollower>())
                    {
                        flowFieldFollower.ValueRW.TargetPosition = mouseWorldPosition;
                        FlowFieldFollowerEnabled.ValueRW = true;
                    }
                #1#
                }
            }
            */

#if GRID_DEBUG
            GridSystemDebug.Instance?.InitializeGrid(gridSystemData);
            GridSystemDebug.Instance?.UpdateGrid(gridSystemData);
#endif
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var gridSystemData = SystemAPI.GetComponentRW<GridSystemData>(state.SystemHandle);
            for (int i = 0; i < FLOW_FIELD_MAP_COUNT; i++)
            {
                gridSystemData.ValueRW.GridMapArray[i].GridEntityArray.Dispose();
            }

            gridSystemData.ValueRW.GridMapArray.Dispose();
            gridSystemData.ValueRW.CostMap.Dispose();
            gridSystemData.ValueRW.TotalGridMapEntityArray.Dispose();
        }

        public static NativeList<RefRW<GridNode>> GetNeighborGridNodeList(
            RefRW<GridNode> currentGridNode,
            NativeArray<RefRW<GridNode>> gridNodeNativeArray,
            int width, int height)
        {
            var neighborGridNodeList = new NativeList<RefRW<GridNode>>(Allocator.Temp);
            var gridNodeX = currentGridNode.ValueRO.X;
            var gridNodeY = currentGridNode.ValueRO.Y;

            var positionLeft = new int2(gridNodeX - 1, gridNodeY + 0);
            var positionRight = new int2(gridNodeX + 1, gridNodeY + 0);
            var positionUp = new int2(gridNodeX + 0, gridNodeY + 1);
            var positionDown = new int2(gridNodeX + 0, gridNodeY - 1);

            var positionLowerLeft = new int2(gridNodeX - 1, gridNodeY - 1);
            var positionLowerRight = new int2(gridNodeX + 1, gridNodeY - 1);
            var positionUpperLeft = new int2(gridNodeX - 1, gridNodeY + 1);
            var positionUpperRight = new int2(gridNodeX + 1, gridNodeY + 1);

            if (IsValidGridPosition(positionLeft, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionLeft, width)]);
            }

            if (IsValidGridPosition(positionRight, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionRight, width)]);
            }

            if (IsValidGridPosition(positionUp, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionUp, width)]);
            }

            if (IsValidGridPosition(positionDown, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionDown, width)]);
            }

            if (IsValidGridPosition(positionLowerLeft, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionLowerLeft, width)]);
            }

            if (IsValidGridPosition(positionLowerRight, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionLowerRight, width)]);
            }

            if (IsValidGridPosition(positionUpperLeft, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionUpperLeft, width)]);
            }

            if (IsValidGridPosition(positionUpperRight, width, height))
            {
                neighborGridNodeList.Add(gridNodeNativeArray[CalculateIndex(positionUpperRight, width)]);
            }

            return neighborGridNodeList;
        }

        public static float2 CalculateVector(int fromX, int fromY, int toX, int toY)
        {
            return new float2(toX - fromX, toY - fromY);
        }

        public static int CalculateIndex(int x, int y, int width)
        {
            return x + y * width;
        }

        public static int CalculateIndex(int2 gridPosition, int width)
        {
            return CalculateIndex(gridPosition.x, gridPosition.y, width);
        }

        public static int2 GetGridPositionFromIndex(int index, int width)
        {
            var y = (int)math.floor(index / width);
            var x = index % width;
            return new int2(x, y);
        }

        public static float3 GetWorldPosition(int x, int y, float gridNodeSize)
        {
            return new float3(
                x * gridNodeSize,
                0f,
                y * gridNodeSize);
        }

        public static float3 GetWorldCenterPosition(int x, int y, float gridNodeSize)
        {
            return new float3(
                x * gridNodeSize + gridNodeSize * 0.5f,
                0f,
                y * gridNodeSize + gridNodeSize * 0.5f);
        }

        public static int2 GetGridPosition(float3 worldPosition, float gridNodeSize)
        {
            return new int2(
                (int)math.floor(worldPosition.x / gridNodeSize),
                (int)math.floor(worldPosition.z / gridNodeSize)
            );
        }

        public static bool IsValidGridPosition(int2 gridPosition, int width, int height)
        {
            return gridPosition.x >= 0 && gridPosition.y >= 0 &&
                   gridPosition.x < width && gridPosition.y < height;
        }

        public static float3 GetWorldMovementVector(float2 vector)
        {
            return new float3(vector.x, 0f, vector.y);
        }

        public static bool IsWall(GridNode gridNode)
        {
            return gridNode.Cost == WALL_COST;
        }

        public static bool IsWall(int2 gridPosition, GridSystemData gridSystemData)
        {
            return gridSystemData.CostMap[CalculateIndex(gridPosition, gridSystemData.Width)] == WALL_COST;
        }

        public static bool IsWall(int2 gridPosition, int width, NativeArray<byte> costMap)
        {
            return costMap[CalculateIndex(gridPosition, width)] == WALL_COST;
        }

        public static bool IsValidWalkableGridPosition(float3 worldPosition, GridSystemData gridSystemData)
        {
            var gridPosition = GetGridPosition(worldPosition, gridSystemData.GridNodeSize);
            return IsValidGridPosition(gridPosition, gridSystemData.Width, gridSystemData.Height) &&
                   !IsWall(gridPosition, gridSystemData);
        }

        public static bool IsValidWalkableGridPosition(float3 worldPosition, int width, int height,
            NativeArray<byte> costMap, float gridNodeSize)
        {
            var gridPosition = GetGridPosition(worldPosition, gridNodeSize);
            return IsValidGridPosition(gridPosition, width, height) && !IsWall(gridPosition, width, costMap);
        }

        [BurstCompile]
        public partial struct InitializeGridJob : IJobEntity
        {
            [ReadOnly] public int GridIndex;
            [ReadOnly] public int2 TargetGridPosition;

            public void Execute(ref GridNode gridNode)
            {
                if (gridNode.GridIndex != GridIndex)
                {
                    return;
                }

                gridNode.Vector = new float2(0, 1);
                if (gridNode.X == TargetGridPosition.x && gridNode.Y == TargetGridPosition.y)
                {
                    // This is the target destination
                    gridNode.Cost = 0;
                    gridNode.BestCost = 0;
                }
                else
                {
                    gridNode.Cost = 1;
                    gridNode.BestCost = int.MaxValue;
                }
            }
        }

        [BurstCompile]
        public partial struct UpdateCostMapJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<GridNode> gridNodeComponentLookup;
            [NativeDisableParallelForRestriction] public NativeArray<byte> CostMap;

            [ReadOnly] public GridMap GridMap;
            [ReadOnly] public CollisionWorld CollisionWorld;
            [ReadOnly] public int Width;
            [ReadOnly] public float GridNodeSize;
            [ReadOnly] public float GridNodeSizeHalf;
            [ReadOnly] public CollisionFilter CollisionFilterWall;
            [ReadOnly] public CollisionFilter CollisionFilterHeavy;

            public void Execute(int index)
            {
                var distanceHitList = new NativeList<DistanceHit>(Allocator.TempJob);
                var gridPosition = GetGridPositionFromIndex(index, Width);
                if (CollisionWorld.OverlapSphere(
                        GetWorldCenterPosition(gridPosition.x, gridPosition.y, GridNodeSize),
                        GridNodeSizeHalf,
                        ref distanceHitList,
                        CollisionFilterWall
                    ))
                {
                    // There is a wall in this grid position
                    var gridNode = gridNodeComponentLookup[GridMap.GridEntityArray[index]];
                    gridNode.Cost = WALL_COST;
                    gridNodeComponentLookup[GridMap.GridEntityArray[index]] = gridNode;
                    CostMap[index] = WALL_COST;
                }

                if (CollisionWorld.OverlapSphere(
                        GetWorldCenterPosition(gridPosition.x, gridPosition.y, GridNodeSize),
                        GridNodeSizeHalf,
                        ref distanceHitList,
                        CollisionFilterHeavy
                    ))
                {
                    // There is a wall in this grid position
                    var gridNode = gridNodeComponentLookup[GridMap.GridEntityArray[index]];
                    gridNode.Cost = HEAVY_COST;
                    gridNodeComponentLookup[GridMap.GridEntityArray[index]] = gridNode;
                    CostMap[index] = HEAVY_COST;
                }

                distanceHitList.Dispose();
            }
        }
    }
}