using System;
using System.Collections.Generic;
using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public event EventHandler OnResourceAmountChanged;

        [SerializeField] private ResourceTypeListSO _resourceTypeListSO;

        private Dictionary<ResourceType, int> _resourceTypeAmountDict;

        private void Awake()
        {
            Instance = this;

            _resourceTypeAmountDict = new();
            foreach (var resourceTypeSo in _resourceTypeListSO.ResourceTypeSOList)
            {
                _resourceTypeAmountDict[resourceTypeSo.ResourceType] = 0;
            }

            AddResourceAmount(ResourceType.Iron, 50);
            AddResourceAmount(ResourceType.Gold, 50);
            AddResourceAmount(ResourceType.Oil, 50);
        }

        public void AddResourceAmount(ResourceType resourceType, int amount)
        {
            _resourceTypeAmountDict[resourceType] += amount;
            OnResourceAmountChanged?.Invoke(this, EventArgs.Empty);
        }

        public int GetResourceAmount(ResourceType resourceType)
        {
            return _resourceTypeAmountDict[resourceType];
        }

        public bool CanSpendResourceAmount(ResourceAmount resourceAmount)
        {
            return _resourceTypeAmountDict[resourceAmount.ResourceType] >= resourceAmount.Amount;
        }

        public bool CanSpendResourceAmount(ResourceAmount[] resourceAmountArray)
        {
            foreach (var resourceAmount in resourceAmountArray)
            {
                if (_resourceTypeAmountDict[resourceAmount.ResourceType] < resourceAmount.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        public void SpendResourceAmount(ResourceAmount resourceAmount)
        {
            _resourceTypeAmountDict[resourceAmount.ResourceType] -= resourceAmount.Amount;
            OnResourceAmountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SpendResourceAmount(ResourceAmount[] resourceAmountArray)
        {
            foreach (var resourceAmount in resourceAmountArray)
            {
                _resourceTypeAmountDict[resourceAmount.ResourceType] -= resourceAmount.Amount;
            }

            OnResourceAmountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}