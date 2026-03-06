using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DotsRts.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderFirst = true)]
    public partial struct AnimationStateSystem : ISystem
    {
        private ComponentLookup<ActiveAnimation> ActiveAnimationComponentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            ActiveAnimationComponentLookup = state.GetComponentLookup<ActiveAnimation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ActiveAnimationComponentLookup.Update(ref state);
            var idleWalkingAnimationStateJob = new IdleWalkingAnimationStateJob
            {
                ActiveAnimationComponentLookup = ActiveAnimationComponentLookup
            };
            idleWalkingAnimationStateJob.ScheduleParallel();

            ActiveAnimationComponentLookup.Update(ref state);
            var aimShootAnimationStateJob = new AimShootAnimationStateJob
            {
                ActiveAnimationComponentLookup = ActiveAnimationComponentLookup
            };
            aimShootAnimationStateJob.ScheduleParallel();

            ActiveAnimationComponentLookup.Update(ref state);
            var meleeAttackAnimationStateJob = new MeleeAttackAnimationStateJob
            {
                ActiveAnimationComponentLookup = ActiveAnimationComponentLookup
            };
            meleeAttackAnimationStateJob.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct IdleWalkingAnimationStateJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] public ComponentLookup<ActiveAnimation> ActiveAnimationComponentLookup;

        public void Execute(in AnimatedMesh animatedMesh,
            in UnitMover unitMover,
            in UnitAnimation unitAnimation)
        {
            var activeAnimation = ActiveAnimationComponentLookup.GetRefRW(animatedMesh.MeshEntity);

            if (unitMover.IsMoving)
            {
                activeAnimation.ValueRW.NextAnimationType = unitAnimation.WalkAnimationType;
            }
            else
            {
                activeAnimation.ValueRW.NextAnimationType = unitAnimation.IdleAnimationType;
            }
        }
    }
    
    [BurstCompile]
    public partial struct AimShootAnimationStateJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] public ComponentLookup<ActiveAnimation> ActiveAnimationComponentLookup;

        public void Execute(ref AnimatedMesh animatedMesh,
            in ShootAttack shootAttack,
            in UnitMover unitMover,
            in Target target,
            in UnitAnimation unitAnimation)
        {
            if (!unitMover.IsMoving && target.TargetEntity != Entity.Null)
            {
                var activeAnimation = ActiveAnimationComponentLookup.GetRefRW(animatedMesh.MeshEntity);
                activeAnimation.ValueRW.NextAnimationType = unitAnimation.AimAnimationType;
            }

            if (shootAttack.OnShoot.IsTriggered)
            {
                var activeAnimation = ActiveAnimationComponentLookup.GetRefRW(animatedMesh.MeshEntity);
                activeAnimation.ValueRW.NextAnimationType = unitAnimation.ShootAnimationType;
            }
        }
    }

    [BurstCompile]
    public partial struct MeleeAttackAnimationStateJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] public ComponentLookup<ActiveAnimation> ActiveAnimationComponentLookup;

        public void Execute(in AnimatedMesh animatedMesh,
            in MeleeAttack meleeAttack,
            in UnitAnimation unitAnimation)
        {
            if (meleeAttack.OnAttacked)
            {
                var activeAnimation = ActiveAnimationComponentLookup.GetRefRW(animatedMesh.MeshEntity);
                activeAnimation.ValueRW.NextAnimationType = unitAnimation.MeleeAnimationType;
            }
        }
    }
}