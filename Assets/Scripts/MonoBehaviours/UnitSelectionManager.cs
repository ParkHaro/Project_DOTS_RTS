using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DotsRts.MonoBehaviours
{
    public class UnitSelectionManager : MonoBehaviour
    {
        public static UnitSelectionManager Instance { get; private set; }

        public event EventHandler OnSelectionAreaStart;
        public event EventHandler OnSelectionAreaEnd;
        public event EventHandler OnSelectedEntitiesChanged;

        private Vector2 _selectionStartMousePosition;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!BuildingPlacementManager.Instance.BuildingTypeSo.IsNone())
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _selectionStartMousePosition = Input.mousePosition;
                
                OnSelectionAreaStart?.Invoke(this, EventArgs.Empty);
            }

            if (Input.GetMouseButtonUp(0))
            {
                Vector2 selectionEndMousePosition = Input.mousePosition;

                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                var entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected>().Build(entityManager);

                var entityArray = entityQuery.ToEntityArray(Allocator.Temp);
                var selectedArray = entityQuery.ToComponentDataArray<Selected>(Allocator.Temp);
                for (var i = 0; i < entityArray.Length; i++)
                {
                    var entity = entityArray[i];
                    entityManager.SetComponentEnabled<Selected>(entity, false);
                    var selected = selectedArray[i];
                    selected.OnDeselected = true;
                    entityManager.SetComponentData(entityArray[i], selected);
                }

                var selectionAreaRect = GetSelectionAreaRect();
                var selectionAreaSize = selectionAreaRect.width + selectionAreaRect.height;
                var multipleSelectionSizeMin = 40f;
                var isMultipleSelection = selectionAreaSize > multipleSelectionSizeMin;
                
                if (isMultipleSelection)
                {
                    entityQuery = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<LocalTransform, Unit>().WithPresent<Selected>().Build(entityManager);
                    entityArray = entityQuery.ToEntityArray(Allocator.Temp);
                    var localTransformArray = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    for (int i = 0; i < localTransformArray.Length; i++)
                    {
                        var unitLocalTransform = localTransformArray[i];
                        var unitScreenPosition = Camera.main.WorldToScreenPoint(unitLocalTransform.Position);
                        if (selectionAreaRect.Contains(unitScreenPosition))
                        {
                            entityManager.SetComponentEnabled<Selected>(entityArray[i], true);
                            var selected = entityManager.GetComponentData<Selected>(entityArray[i]);
                            selected.OnSelected = true;
                            entityManager.SetComponentData(entityArray[i], selected);
                        }
                    }
                }
                else
                {
                    entityQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                    var physicsWorldSingleton = entityQuery.GetSingleton<PhysicsWorldSingleton>();
                    var collisionWorld = physicsWorldSingleton.CollisionWorld;
                    UnityEngine.Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                    var raycastInput = new RaycastInput
                    {
                        Start = cameraRay.GetPoint(0f),
                        End = cameraRay.GetPoint(9999f),
                        Filter = new CollisionFilter
                        {
                            BelongsTo = ~0u,
                            CollidesWith = 1u << GameAssets.UNITS_LAYER | 1u << GameAssets.BUILDINGS_LAYER,
                            GroupIndex = 0,
                        }
                    };
                    if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit raycastHit))
                    {
                        if (entityManager.HasComponent<Selected>(raycastHit.Entity))
                        {
                            entityManager.SetComponentEnabled<Selected>(raycastHit.Entity, true);
                            var selected = entityManager.GetComponentData<Selected>(raycastHit.Entity);
                            selected.OnSelected = true;
                            entityManager.SetComponentData(raycastHit.Entity, selected);
                        }
                    }
                }

                OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);
                OnSelectedEntitiesChanged?.Invoke(this, EventArgs.Empty);
            }

            if (Input.GetMouseButtonDown(1))
            {
                var mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();

                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                var entityQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                var physicsWorldSingleton = entityQuery.GetSingleton<PhysicsWorldSingleton>();
                var collisionWorld = physicsWorldSingleton.CollisionWorld;
                UnityEngine.Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                var raycastInput = new RaycastInput
                {
                    Start = cameraRay.GetPoint(0f),
                    End = cameraRay.GetPoint(9999f),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = 1u << GameAssets.UNITS_LAYER | 1u << GameAssets.BUILDINGS_LAYER,
                        GroupIndex = 0,
                    }
                };

                var isAttackingSingleTarget = false;
                if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit raycastHit))
                {
                    if (entityManager.HasComponent<Faction>(raycastHit.Entity))
                    {
                        // Hit a Unit
                        var faction = entityManager.GetComponentData<Faction>(raycastHit.Entity);
                        if (faction.FactionType == FactionType.Zombie)
                        {
                            // Right clicking on a zombie
                            isAttackingSingleTarget = true;

                            entityQuery = new EntityQueryBuilder(Allocator.Temp)
                                .WithAll<Selected>().WithPresent<TargetOverride>().Build(entityManager);

                            var entityArray = entityQuery.ToEntityArray(Allocator.Temp);
                            var targetOverrideArray = entityQuery.ToComponentDataArray<TargetOverride>(Allocator.Temp);
                            for (int i = 0; i < targetOverrideArray.Length; i++)
                            {
                                var targetOverride = targetOverrideArray[i];
                                targetOverride.TargetEntity = raycastHit.Entity;
                                targetOverrideArray[i] = targetOverride;
                                entityManager.SetComponentEnabled<MoveOverride>(entityArray[i], false);
                            }

                            entityQuery.CopyFromComponentDataArray(targetOverrideArray);
                        }
                    }
                }

                if (!isAttackingSingleTarget)
                {
                    entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<UnitMover, Selected>()
                        .WithPresent<MoveOverride, TargetOverride, TargetPositionPathQueued,
                            FlowFieldPathRequest, FlowFieldFollower>().Build(entityManager);

                    var entityArray = entityQuery.ToEntityArray(Allocator.Temp);
                    var moveOverrideArray = entityQuery.ToComponentDataArray<MoveOverride>(Allocator.Temp);
                    var targetOverrideArray = entityQuery.ToComponentDataArray<TargetOverride>(Allocator.Temp);
                    var targetPositionPathQueuedArray =
                        entityQuery.ToComponentDataArray<TargetPositionPathQueued>(Allocator.Temp);

                    var movePositionArray = GenerateMovePositionArray(mouseWorldPosition, moveOverrideArray.Length);
                    for (int i = 0; i < moveOverrideArray.Length; i++)
                    {
                        var moveOverride = moveOverrideArray[i];
                        moveOverride.TargetPosition = movePositionArray[i];
                        moveOverrideArray[i] = moveOverride;
                        entityManager.SetComponentEnabled<MoveOverride>(entityArray[i], true);

                        var targetOverride = targetOverrideArray[i];
                        targetOverride.TargetEntity = Entity.Null;
                        targetOverrideArray[i] = targetOverride;

                        var targetPositionPathQueued = targetPositionPathQueuedArray[i];
                        targetPositionPathQueued.TargetPosition = movePositionArray[i];
                        targetPositionPathQueuedArray[i] = targetPositionPathQueued;
                        entityManager.SetComponentEnabled<TargetPositionPathQueued>(entityArray[i], true);

                        entityManager.SetComponentEnabled<FlowFieldPathRequest>(entityArray[i], false);
                        entityManager.SetComponentEnabled<FlowFieldFollower>(entityArray[i], false);
                    }

                    entityQuery.CopyFromComponentDataArray(moveOverrideArray);
                    entityQuery.CopyFromComponentDataArray(targetOverrideArray);
                    entityQuery.CopyFromComponentDataArray(targetPositionPathQueuedArray);
                }

                // Handle Barrakcs Rally Position
                entityQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Selected, BuildingBarracks, LocalTransform>().Build(entityManager);

                var buildingBarracksArray = entityQuery.ToComponentDataArray<BuildingBarracks>(Allocator.Temp);
                var localTransformArray = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                for (int i = 0; i < localTransformArray.Length; i++)
                {
                    var buildingBarracks = buildingBarracksArray[i];
                    buildingBarracks.RallyPositionOffset = (float3)mouseWorldPosition - localTransformArray[i].Position;
                    buildingBarracksArray[i] = buildingBarracks;
                }

                entityQuery.CopyFromComponentDataArray(buildingBarracksArray);
            }
        }

        public Rect GetSelectionAreaRect()
        {
            var selectionEndMousePosition = Input.mousePosition;
            var lowerLeftConer = new Vector2(
                Mathf.Min(_selectionStartMousePosition.x, selectionEndMousePosition.x),
                Mathf.Min(_selectionStartMousePosition.y, selectionEndMousePosition.y));

            var upperRightCorner = new Vector2(
                Mathf.Max(_selectionStartMousePosition.x, selectionEndMousePosition.x),
                Mathf.Max(_selectionStartMousePosition.y, selectionEndMousePosition.y));
            return new Rect(
                lowerLeftConer.x,
                lowerLeftConer.y,
                upperRightCorner.x - lowerLeftConer.x,
                upperRightCorner.y - lowerLeftConer.y
            );
        }

        private NativeArray<float3> GenerateMovePositionArray(float3 targetPosition, int positionCount)
        {
            var positionArray = new NativeArray<float3>(positionCount, Allocator.Temp);
            if (positionCount == 0)
            {
                return positionArray;
            }

            positionArray[0] = targetPosition;
            if (positionCount == 1)
            {
                return positionArray;
            }

            var ringSize = 2.2f;
            var ring = 0;
            int positionIndex = 1;

            while (positionIndex < positionCount)
            {
                var ringPositionCount = 3 + ring * 2;
                for (int i = 0; i < ringPositionCount; i++)
                {
                    var angle = i * (math.PI2 / ringPositionCount);
                    var ringVector = math.rotate(quaternion.RotateY(angle), new float3(ringSize * (ring + 1), 0, 0));
                    var ringPosition = targetPosition + ringVector;

                    positionArray[positionIndex] = ringPosition;
                    positionIndex++;

                    if (positionIndex >= positionCount)
                    {
                        break;
                    }
                }

                ring++;
            }

            return positionArray;
        }
    }
}