using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using BoxCollider = UnityEngine.BoxCollider;
using Material = UnityEngine.Material;

namespace DotsRts.MonoBehaviours
{
    public class BuildingPlacementManager : MonoBehaviour
    {
        public static BuildingPlacementManager Instance { get; private set; }
        public event EventHandler OnActiveBuildingTypeSOChanged;

        [SerializeField] private BuildingTypeSO _buildingTypeSo;
        [SerializeField] private Material _ghostMaterial;

        private Transform _ghostTransform;

        public BuildingTypeSO BuildingTypeSo
        {
            get => _buildingTypeSo;
            set
            {
                _buildingTypeSo = value;
                if (_ghostTransform != null)
                {
                    Destroy(_ghostTransform.gameObject);
                }

                if (!_buildingTypeSo.IsNone())
                {
                    _ghostTransform = Instantiate(_buildingTypeSo.VisualPrefab);
                    foreach (var meshRenderer in _ghostTransform.GetComponentsInChildren<MeshRenderer>())
                    {
                        meshRenderer.material = _ghostMaterial;
                    }
                }

                OnActiveBuildingTypeSOChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (_ghostTransform != null)
            {
                _ghostTransform.position = MouseWorldPosition.Instance.GetPosition();
            }

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (_buildingTypeSo.IsNone())
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                BuildingTypeSo = GameAssets.Instance.BuildingTypeListSO.None;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (ResourceManager.Instance.CanSpendResourceAmount(_buildingTypeSo.BuildCostResourceAmountArray))
                {
                    if (CanPlaceBuilding())
                    {
                        ResourceManager.Instance.SpendResourceAmount(_buildingTypeSo.BuildCostResourceAmountArray);

                        var mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();
                        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                        var entityQuery = entityManager.CreateEntityQuery(typeof(EntitiesReferences));
                        var entitiesReferences = entityQuery.GetSingleton<EntitiesReferences>();

                        // var spawnedEntity = entityManager.Instantiate(_buildingTypeSo.GetPrefabEntity(entitiesReferences));
                        // entityManager.SetComponentData(spawnedEntity, LocalTransform.FromPosition(mouseWorldPosition));

                        var buildingConstructionVisualEntity =
                            entityManager.Instantiate(_buildingTypeSo.GetVisualPrefabEntity(entitiesReferences));
                        entityManager.SetComponentData(buildingConstructionVisualEntity,
                            LocalTransform.FromPosition(mouseWorldPosition + new Vector3(0, _buildingTypeSo.ConstructionYOffset, 0)));

                        var buildingConstructionEntity =
                            entityManager.Instantiate(entitiesReferences.BuildingConstructionPrefabEntity);
                        entityManager.SetComponentData(buildingConstructionEntity,
                            LocalTransform.FromPosition(mouseWorldPosition));
                        entityManager.SetComponentData(buildingConstructionEntity, new BuildingConstruction
                        {
                            BuildingType = _buildingTypeSo.BuildingType,
                            ConstructionTimer = 0f,
                            ConstructionTimerMax = _buildingTypeSo.BuildingConstructionTimerMax,
                            FinalPrefabEntity = _buildingTypeSo.GetPrefabEntity(entitiesReferences),
                            VisualEntity = buildingConstructionVisualEntity,
                            StartPosition = mouseWorldPosition + new Vector3(0, _buildingTypeSo.ConstructionYOffset, 0),
                            EndPosition = mouseWorldPosition,
                        });
                    }
                }
            }
        }

        private bool CanPlaceBuilding()
        {
            var mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            var entityQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
            var physicsWorldSingleton = entityQuery.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.CollisionWorld;
            var collisionFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << GameAssets.BUILDINGS_LAYER | 1u << GameAssets.DEFAULT_LAYER,
                GroupIndex = 0,
            };

            var boxCollider = _buildingTypeSo.Prefab.GetComponent<BoxCollider>();
            var bonusExtents = 1.1f;
            var distanceHitList = new NativeList<DistanceHit>(Allocator.Temp);
            if (collisionWorld.OverlapBox(mouseWorldPosition, quaternion.identity,
                    boxCollider.size * 0.5f * bonusExtents, ref distanceHitList, collisionFilter))
            {
                return false;
            }

            distanceHitList.Clear();
            if (collisionWorld.OverlapSphere(mouseWorldPosition,
                    _buildingTypeSo.BuildingDistanceMin,
                    ref distanceHitList, collisionFilter))
            {
                foreach (var distanceHit in distanceHitList)
                {
                    if (entityManager.HasComponent<BuildingTypeSOHolder>(distanceHit.Entity))
                    {
                        var buildingTypeSoHolder =
                            entityManager.GetComponentData<BuildingTypeSOHolder>(distanceHit.Entity);
                        if (buildingTypeSoHolder.BuildingType == _buildingTypeSo.BuildingType)
                        {
                            // Same type too close
                            return false;
                        }
                    }
                    
                    if (entityManager.HasComponent<BuildingConstruction>(distanceHit.Entity))
                    {
                        var buildingTypeSoHolder =
                            entityManager.GetComponentData<BuildingTypeSOHolder>(distanceHit.Entity);
                        if (buildingTypeSoHolder.BuildingType == _buildingTypeSo.BuildingType)
                        {
                            // Same type too close
                            return false;
                        }
                    }
                }
            }

            if (_buildingTypeSo is BuildingResourceHarvesterTypeSO buildingResourceHarvesterTypeSO)
            {
                var hasValidNearbyResourceNode = false;
                if (collisionWorld.OverlapSphere(mouseWorldPosition,
                        buildingResourceHarvesterTypeSO.HarvestDistance,
                        ref distanceHitList, collisionFilter))
                {
                    foreach (var distanceHit in distanceHitList)
                    {
                        if (entityManager.HasComponent<ResourceTypeSOHolder>(distanceHit.Entity))
                        {
                            var resourceTypeSoHolder =
                                entityManager.GetComponentData<ResourceTypeSOHolder>(distanceHit.Entity);
                            if (resourceTypeSoHolder.resourceType ==
                                buildingResourceHarvesterTypeSO.HarvestableResourceType)
                            {
                                // Nearby valid resource node
                                hasValidNearbyResourceNode = true;
                                break;
                            }
                        }
                    }
                }

                if (!hasValidNearbyResourceNode)
                {
                    return false;
                }
            }

            return true;
        }
    }
}