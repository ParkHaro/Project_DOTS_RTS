using DotsRts.MonoBehaviours;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DotsRts.Systems
{
    public partial struct BuildingHarvesterSystem : ISystem
    {
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var buildingHarvester
                     in SystemAPI.Query<
                         RefRW<BuildingHarvester>>())
            {
                buildingHarvester.ValueRW.HarvestTimer -= SystemAPI.Time.DeltaTime;
                if (buildingHarvester.ValueRW.HarvestTimer <= 0f)
                {
                    buildingHarvester.ValueRW.HarvestTimer = buildingHarvester.ValueRW.HarvestTimerMax;

                    ResourceManager.Instance.AddResourceAmount(buildingHarvester.ValueRO.ResourceType, 1);
                }
            }
        }
    }
}