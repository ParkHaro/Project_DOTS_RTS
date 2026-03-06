using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct LoseTargetSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (localTransform,
                         target,
                         loseTarget,
                         targetOverride)
                     in SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<Target>,
                         RefRO<LoseTarget>,
                         RefRO<TargetOverride>>())
            {
                if (target.ValueRO.TargetEntity == Entity.Null)
                {
                    continue;
                }

                if (targetOverride.ValueRO.TargetEntity != Entity.Null)
                {
                    continue;
                }

                var targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(target.ValueRO.TargetEntity);
                var targetDistance = math.distance(localTransform.ValueRO.Position, targetLocalTransform.Position);
                if (targetDistance > loseTarget.ValueRO.LoseTargetDistance)
                {
                    target.ValueRW.TargetEntity = Entity.Null;
                }
            }
        }
    }
}