using System;
using Unity.Entities;
using UnityEngine;

namespace DotsRts
{
    public enum UnitType
    {
        None,
        Soldier,
        Scout,
        Zombie,
    }

    [CreateAssetMenu]
    public class UnitTypeSO : ScriptableObject
    {
        public UnitType UnitType;
        public Transform RagdollPrefab;
        public float ProgressMax;
        public Sprite Sprite;
        public ResourceAmount[] SpawnCostResourceAmountArray;

        public Entity GetPrefabEntity(EntitiesReferences entitiesReferences)
        {
            switch (UnitType)
            {
                case UnitType.Soldier: return entitiesReferences.SoldierPrefabEntity;
                case UnitType.Scout: return entitiesReferences.ScoutPrefabEntity;
                case UnitType.Zombie: return entitiesReferences.ZombiePrefabEntity;
                default:
                    Debug.Log("Prefab Entity not found for UnitType: " + UnitType);
                    return Entity.Null;
            }
        }
    }
}