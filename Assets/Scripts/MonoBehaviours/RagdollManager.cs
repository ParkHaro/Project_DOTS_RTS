using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class RagdollManager : MonoBehaviour
    {
        [SerializeField] private UnitTypeListSO _unitTypeListSo;
        
        private void Start()
        {
            DOTSEventsManager.Instance.OnHealthDead += DOTSEventsManager_OnHealthDead;
        }

        private void DOTSEventsManager_OnHealthDead(object sender, EventArgs e)
        {
            var entity = (Entity)sender;
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (entityManager.HasComponent<UnitTypeHolder>(entity))
            {
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                var unitTypeHolder = entityManager.GetComponentData<UnitTypeHolder>(entity);
                var unitTypeSo = _unitTypeListSo.GetUnitTypeSO(unitTypeHolder.UnitType);
                Instantiate(unitTypeSo.RagdollPrefab, localTransform.Position, localTransform.Rotation);
            }
            
        }
    }
}