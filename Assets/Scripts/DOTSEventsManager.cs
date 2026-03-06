using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DotsRts
{
    public class DOTSEventsManager : MonoBehaviour
    {
        public static DOTSEventsManager Instance { get; private set; }

        public event EventHandler OnBarracksUnitQueueChanged;
        public event EventHandler OnHQDead;
        public event EventHandler OnHealthDead;

        private void Awake()
        {
            Instance = this;
        }

        public void TriggerOnBarracksUnitQueueChanged(NativeList<Entity> entityNativeList)
        {
            foreach (var entity in entityNativeList)
            {
                OnBarracksUnitQueueChanged?.Invoke(entity, EventArgs.Empty);
            }
        }

        public void TriggerOnHQDead()
        {
            OnHQDead?.Invoke(this, EventArgs.Empty);
        }
        
        public void TriggerOnHealthDead(NativeList<Entity> entityNativeList)
        {
            foreach (var entity in entityNativeList)
            {
                OnHealthDead?.Invoke(entity, EventArgs.Empty);
            }
        }
    }
}