using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DotsRts.Systems
{
    public partial struct RandomWalkingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (randomWalking,
                         targetPositionPathQueued,
                         targetPositionPathQueuedEnabled,
                         localTransform)
                     in SystemAPI.Query<
                         RefRW<RandomWalking>,
                         RefRW<TargetPositionPathQueued>,
                         EnabledRefRW<TargetPositionPathQueued>,
                         RefRO<LocalTransform>>().WithPresent<TargetPositionPathQueued>())
            {
                if (math.distancesq(localTransform.ValueRO.Position, randomWalking.ValueRO.TargetPosition)
                    < UnitMoverSystem.REACHED_TARGET_POSITION_DISTANCE_SQ)
                {
                    var random = randomWalking.ValueRO.Random;
                    var randomDirection = new float3(random.NextFloat(-1f, 1f), 0, random.NextFloat(-1f, 1f));
                    randomDirection = math.normalize(randomDirection);

                    randomWalking.ValueRW.TargetPosition =
                        randomWalking.ValueRO.OriginPosition +
                        randomDirection * random.NextFloat(randomWalking.ValueRO.DistanceMin,
                            randomWalking.ValueRO.DistanceMax);

                    randomWalking.ValueRW.Random = random;
                }
                else
                {
                    targetPositionPathQueued.ValueRW.TargetPosition = randomWalking.ValueRO.TargetPosition;
                    targetPositionPathQueuedEnabled.ValueRW = true;
                }
            }
        }
    }
}