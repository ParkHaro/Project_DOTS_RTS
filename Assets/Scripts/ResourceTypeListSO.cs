using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DotsRts
{
    [CreateAssetMenu]
    public class ResourceTypeListSO : ScriptableObject
    {
        public List<ResourceTypeSO> ResourceTypeSOList;
    }
}