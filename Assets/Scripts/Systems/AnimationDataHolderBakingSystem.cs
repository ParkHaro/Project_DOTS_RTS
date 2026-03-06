using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace DotsRts.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct AnimationDataHolderBakingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AnimationDataHolderObjectData>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            AnimationDataListSO animationDataListSo = null;
            foreach (var animationDataHolderObjectData in SystemAPI.Query<RefRO<AnimationDataHolderObjectData>>())
            {
                animationDataListSo = animationDataHolderObjectData.ValueRO.AnimationDataListSO.Value;
            }

            var blobAssetDataDictionary = new Dictionary<AnimationType, int[]>();

            foreach (AnimationType animationType in System.Enum.GetValues(typeof(AnimationType)))
            {
                var animationDataSo = animationDataListSo.GetAnimationDataSO(animationType);
                blobAssetDataDictionary[animationType] = new int[animationDataSo.MeshArray.Length];
            }

            foreach (var (animationDataHolderSubEntity,
                         materialMeshInfo)
                     in SystemAPI.Query<
                         RefRO<AnimationDataHolderSubEntity>,
                         RefRO<MaterialMeshInfo>>())
            {
                blobAssetDataDictionary[animationDataHolderSubEntity.ValueRO.AnimationType][
                    animationDataHolderSubEntity.ValueRO.MeshIndex] = materialMeshInfo.ValueRO.Mesh;
            }

            foreach (var animationDataHolder in SystemAPI.Query<RefRW<AnimationDataHolder>>())
            {
                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var animationDataBlobArray = ref blobBuilder.ConstructRoot<BlobArray<AnimationData>>();

                var animationDataBlobBuilderArray = blobBuilder.Allocate(ref animationDataBlobArray,
                    System.Enum.GetValues(typeof(AnimationType)).Length);

                var index = 0;
                foreach (AnimationType animationType in System.Enum.GetValues(typeof(AnimationType)))
                {
                    var animationDataSo = animationDataListSo.GetAnimationDataSO(animationType);
                    var blobBuilderArray = blobBuilder.Allocate<int>(
                        ref animationDataBlobBuilderArray[index].intMeshIdBlobArray,
                        animationDataSo.MeshArray.Length);

                    animationDataBlobBuilderArray[index].FrameTimerMax = animationDataSo.FrameTimerMax;
                    animationDataBlobBuilderArray[index].FrameMax = animationDataSo.MeshArray.Length;

                    for (int i = 0; i < animationDataSo.MeshArray.Length; i++)
                    {
                        blobBuilderArray[i] = blobAssetDataDictionary[animationType][i];
                    }

                    index++;
                }

                animationDataHolder.ValueRW.AnimationDataBlobArrayBlobAssetReference =
                    blobBuilder.CreateBlobAssetReference<BlobArray<AnimationData>>(Allocator.Persistent);

                blobBuilder.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var animationDataHolder in SystemAPI.Query<RefRW<AnimationDataHolder>>())
            {
                animationDataHolder.ValueRW.AnimationDataBlobArrayBlobAssetReference.Dispose();
            }
        }
    }
}