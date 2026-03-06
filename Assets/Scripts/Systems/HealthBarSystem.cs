using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DotsRts.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct HealthBarSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<PostTransformMatrix> _postTransformMatrixLookup;

        public void OnCreate(ref SystemState state)
        {
            _localTransformLookup = state.GetComponentLookup<LocalTransform>();
            _healthLookup = state.GetComponentLookup<Health>(true);
            _postTransformMatrixLookup = state.GetComponentLookup<PostTransformMatrix>();
        }


        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var cameraForward = Vector3.zero;
            if (Camera.main != null)
            {
                cameraForward = Camera.main.transform.forward;
            }

            _localTransformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _postTransformMatrixLookup.Update(ref state);
            var healthBarJob = new HealthBarJob
            {
                CameraForward = cameraForward,
                LocalTransformLookup = _localTransformLookup,
                HealthLookup = _healthLookup,
                PostTransformMatrixLookup = _postTransformMatrixLookup,
            };
            healthBarJob.ScheduleParallel();

            /*
            foreach (var (localTransform,
                         healthBar)
                     in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRO<HealthBar>>())
            {
                var parentLocalTransform = SystemAPI.GetComponent<LocalTransform>(healthBar.ValueRO.HealthEntity);
                if (Mathf.Approximately(localTransform.ValueRO.Scale, 1f))
                {
                    localTransform.ValueRW.Rotation = parentLocalTransform.InverseTransformRotation(
                        quaternion.LookRotationSafe(cameraForward, math.up()));
                }

                var health = SystemAPI.GetComponent<Health>(healthBar.ValueRO.HealthEntity);
                if (!health.OnHealthChanged)
                {
                    continue;
                }

                var healthNormalized = (float)health.HealthAmount / health.HealthAmountMax;

                localTransform.ValueRW.Scale = Mathf.Approximately(healthNormalized, 1f) ? 0f : 1f;

                var barVisualPostTransformMatrix =
                    SystemAPI.GetComponentRW<PostTransformMatrix>(healthBar.ValueRO.BarVisualEntity);
                barVisualPostTransformMatrix.ValueRW.Value = float4x4.Scale(healthNormalized, 1, 1);
            }
            */
        }
    }

    [BurstCompile]
    public partial struct HealthBarJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<Health> HealthLookup;

        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;

        public float3 CameraForward;

        public void Execute(in HealthBar healthBar, Entity entity)
        {
            var localTransform = LocalTransformLookup.GetRefRW(entity);
            var parentLocalTransform = LocalTransformLookup[healthBar.HealthEntity];
            if (Mathf.Approximately(localTransform.ValueRO.Scale, 1f))
            {
                localTransform.ValueRW.Rotation = parentLocalTransform.InverseTransformRotation(
                    quaternion.LookRotationSafe(CameraForward, math.up()));
            }

            var health = HealthLookup[healthBar.HealthEntity];
            if (!health.OnHealthChanged)
            {
                return;
            }

            var healthNormalized = (float)health.HealthAmount / health.HealthAmountMax;

            localTransform.ValueRW.Scale = Mathf.Approximately(healthNormalized, 1f) ? 0f : 1f;

            var barVisualPostTransformMatrix =
                PostTransformMatrixLookup.GetRefRW(healthBar.BarVisualEntity);
            barVisualPostTransformMatrix.ValueRW.Value = float4x4.Scale(healthNormalized, 1, 1);
        }
    }
}