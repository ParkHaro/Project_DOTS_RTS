using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace DotsRts.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial struct ResetEventSystem : ISystem
    {
        private NativeArray<JobHandle> _jobHandleNativeArray;
        private NativeList<Entity> _onBarracksUnitQueueChangedEntityList;
        private NativeList<Entity> _onHealthDeadEntityList;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BuildingHQ>();
            _jobHandleNativeArray = new NativeArray<JobHandle>(3, Allocator.Persistent);
            _onBarracksUnitQueueChangedEntityList = new NativeList<Entity>(Allocator.Persistent);
            _onHealthDeadEntityList = new NativeList<Entity>(Allocator.Persistent);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<BuildingHQ>())
            {
                var hqHealth = SystemAPI.GetComponent<Health>(SystemAPI.GetSingletonEntity<BuildingHQ>());
                if (hqHealth.OnDead)
                {
                    DOTSEventsManager.Instance.TriggerOnHQDead();
                }
            }

            _jobHandleNativeArray[0] = new ResetShootAttackEventsJob().ScheduleParallel(state.Dependency);
            _jobHandleNativeArray[1] = new ResetSelectedEventsJob().ScheduleParallel(state.Dependency);
            _jobHandleNativeArray[2] = new ResetMeleeAttackEventsJob().ScheduleParallel(state.Dependency);

            _onHealthDeadEntityList.Clear();
            new ResetHealthEventsJob
            {
                OnHealthDeadEntityList = _onHealthDeadEntityList.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency).Complete();

            DOTSEventsManager.Instance.TriggerOnHealthDead(_onHealthDeadEntityList);
            
            _onBarracksUnitQueueChangedEntityList.Clear();
            new ResetBuildingBarracksEventsJob
            {
                OnUnitQueueChangedEntityList = _onBarracksUnitQueueChangedEntityList.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency).Complete();

            DOTSEventsManager.Instance.TriggerOnBarracksUnitQueueChanged(_onBarracksUnitQueueChangedEntityList);

            state.Dependency = JobHandle.CombineDependencies(_jobHandleNativeArray);
        }

        public void OnDestroy(ref SystemState state)
        {
            _jobHandleNativeArray.Dispose();
            _onBarracksUnitQueueChangedEntityList.Dispose();
            _onHealthDeadEntityList.Dispose();
        }
    }

    [BurstCompile]
    public partial struct ResetShootAttackEventsJob : IJobEntity
    {
        public void Execute(ref ShootAttack shootAttack)
        {
            shootAttack.OnShoot.IsTriggered = false;
        }
    }

    [BurstCompile]
    public partial struct ResetHealthEventsJob : IJobEntity
    {
        public NativeList<Entity>.ParallelWriter OnHealthDeadEntityList;

        public void Execute(ref Health health, Entity entity)
        {
            if (health.OnDead)
            {
                OnHealthDeadEntityList.AddNoResize(entity);
            }
            
            health.OnHealthChanged = false;
            health.OnDead = false;
        }
    }

    [BurstCompile]
    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    public partial struct ResetSelectedEventsJob : IJobEntity
    {
        public void Execute(ref Selected selected)
        {
            selected.OnSelected = false;
            selected.OnDeselected = false;
        }
    }

    [BurstCompile]
    public partial struct ResetMeleeAttackEventsJob : IJobEntity
    {
        public void Execute(ref MeleeAttack meleeAttack)
        {
            meleeAttack.OnAttacked = false;
        }
    }

    [BurstCompile]
    public partial struct ResetBuildingBarracksEventsJob : IJobEntity
    {
        public NativeList<Entity>.ParallelWriter OnUnitQueueChangedEntityList;

        public void Execute(ref BuildingBarracks buildingBarracks, Entity entity)
        {
            if (buildingBarracks.OnUnitQueueChanged)
            {
                OnUnitQueueChangedEntityList.AddNoResize(entity);
            }

            buildingBarracks.OnUnitQueueChanged = false;
        }
    }
}