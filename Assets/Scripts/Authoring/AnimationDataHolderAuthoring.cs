using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace DotsRts
{
    public class AnimationDataHolderAuthoring : MonoBehaviour
    {
        public AnimationDataListSO AnimationDataListSO;
        public Material DefaultMaterial;

        private class AnimationDataHolderAuthoringBaker : Baker<AnimationDataHolderAuthoring>
        {
            public override void Bake(AnimationDataHolderAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var animationDataHolder = new AnimationDataHolder();

                var index = 0;
                foreach (AnimationType animationType in System.Enum.GetValues(typeof(AnimationType)))
                {
                    var animationDataSO = authoring.AnimationDataListSO.GetAnimationDataSO(animationType);

                    for (int i = 0; i < animationDataSO.MeshArray.Length; i++)
                    {
                        var mesh = animationDataSO.MeshArray[i];
                        var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.None, false);

                        AddComponent(additionalEntity, new MaterialMeshInfo());
                        AddComponent(additionalEntity, new RenderMeshUnmanaged
                        {
                            materialForSubMesh = authoring.DefaultMaterial,
                            mesh = mesh,
                        });
                        AddComponent(additionalEntity, new AnimationDataHolderSubEntity
                        {
                            AnimationType = animationType,
                            MeshIndex = i,
                        });
                    }

                    index++;
                }
                
                AddComponent(entity, new AnimationDataHolderObjectData
                {
                    AnimationDataListSO = authoring.AnimationDataListSO,
                });

                AddComponent(entity, animationDataHolder);
            }
        }
    }

    public struct AnimationDataHolderObjectData : IComponentData
    {
        public UnityObjectRef<AnimationDataListSO> AnimationDataListSO;
    }

    public struct AnimationDataHolderSubEntity : IComponentData
    {
        public AnimationType AnimationType;
        public int MeshIndex;
    }

    public struct AnimationDataHolder : IComponentData
    {
        public BlobAssetReference<BlobArray<AnimationData>> AnimationDataBlobArrayBlobAssetReference;
    }

    public struct AnimationData
    {
        public float FrameTimerMax;
        public int FrameMax;
        public BlobArray<int> intMeshIdBlobArray;
    }
}