using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using WarehouseRefillPlus.UI;
using WarehouseRefillPlus.Utilities;

namespace WarehouseRefillPlus.Patches
{
    /// <summary>
    /// Replaces MarketAppUIEnhancer.GetMaxBoxCapacity with a cached implementation.
    ///
    /// The original method searches for IDManager and RackManager and then scans every
    /// rack slot for every SalesItem created by the market. With many products/racks,
    /// entering the market becomes an O(products * rackSlots) operation.
    ///
    /// This patch builds one rack-capacity snapshot and serves subsequent product
    /// queries from dictionaries, reducing the expensive part to O(rackSlots + products).
    /// </summary>
    [HarmonyPatch(typeof(MarketAppUIEnhancer), "GetMaxBoxCapacity")]
    internal static class MarketAppUIEnhancerPerformancePatch
    {
        private const float SnapshotLifetimeSeconds = 10f;
        private const int SafeFallbackCapacity = 99;

        private static readonly Dictionary<int, int> CapacityByProduct = new();
        private static readonly Dictionary<int, int> SingleSlotCapacityByProduct = new();

        private static RackManager _rackManager;
        private static IDManager _idManager;
        private static bool _hasSnapshot;
        private static int _cachedRackCount = -1;
        private static float _snapshotBuiltAt = -1000f;

        [HarmonyPrefix]
        private static bool Prefix(int productId, ref int __result)
        {
            try
            {
                __result = GetMaxBoxCapacityCached(productId);
            }
            catch
            {
                // Preserve the original method's defensive behavior without logging in
                // this hot path. Repeated exception logging would itself hurt performance.
                __result = SafeFallbackCapacity;
            }

            return false;
        }

        private static int GetMaxBoxCapacityCached(int productId)
        {
            if (productId <= 0)
            {
                return SafeFallbackCapacity;
            }

            if (!EnsureManagers())
            {
                return SafeFallbackCapacity;
            }

            int rackCount = _rackManager.m_Racks?.Count ?? 0;
            bool snapshotExpired = Time.unscaledTime - _snapshotBuiltAt >= SnapshotLifetimeSeconds;

            if (!_hasSnapshot || rackCount != _cachedRackCount || snapshotExpired)
            {
                RebuildSnapshot(rackCount);
            }

            if (CapacityByProduct.TryGetValue(productId, out int totalCapacity))
            {
                return totalCapacity;
            }

            return GetSingleSlotCapacity(productId);
        }

        private static bool EnsureManagers()
        {
            bool managerMissing = _rackManager == null || _rackManager.gameObject == null ||
                                  _idManager == null || _idManager.gameObject == null;

            if (!managerMissing)
            {
                return true;
            }

            RackManager newRackManager = UnityEngine.Object.FindFirstObjectByType<RackManager>();
            IDManager newIdManager = UnityEngine.Object.FindFirstObjectByType<IDManager>();

            if (newRackManager == null || newIdManager == null)
            {
                return false;
            }

            bool managerChanged = newRackManager != _rackManager || newIdManager != _idManager;
            _rackManager = newRackManager;
            _idManager = newIdManager;

            if (managerChanged)
            {
                CapacityByProduct.Clear();
                SingleSlotCapacityByProduct.Clear();
                _hasSnapshot = false;
                _cachedRackCount = -1;
            }

            return true;
        }

        private static void RebuildSnapshot(int rackCount)
        {
            Dictionary<int, int> newCapacities = new Dictionary<int, int>();

            for (int rackIndex = 0; rackIndex < rackCount; rackIndex++)
            {
                Rack rack = _rackManager.m_Racks[rackIndex];
                if (rack == null || rack.RackSlots == null)
                {
                    continue;
                }

                int slotCount = rack.RackSlots.Count;
                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    RackSlot rackSlot = rack.RackSlots[slotIndex];
                    if (rackSlot == null || rackSlot.Data == null)
                    {
                        continue;
                    }

                    int productId = rackSlot.Data.ProductID;
                    if (productId <= 0)
                    {
                        continue;
                    }

                    int slotCapacity = GetSingleSlotCapacity(productId);
                    int currentBoxCount = rackSlot.Data.BoxCount;
                    if (currentBoxCount > slotCapacity)
                    {
                        slotCapacity = currentBoxCount;
                    }

                    if (newCapacities.TryGetValue(productId, out int currentCapacity))
                    {
                        newCapacities[productId] = currentCapacity + slotCapacity;
                    }
                    else
                    {
                        newCapacities.Add(productId, slotCapacity);
                    }
                }
            }

            CapacityByProduct.Clear();
            foreach (KeyValuePair<int, int> entry in newCapacities)
            {
                CapacityByProduct.Add(entry.Key, entry.Value);
            }

            _cachedRackCount = rackCount;
            _snapshotBuiltAt = Time.unscaledTime;
            _hasSnapshot = true;
        }

        private static int GetSingleSlotCapacity(int productId)
        {
            if (SingleSlotCapacityByProduct.TryGetValue(productId, out int cachedCapacity))
            {
                return cachedCapacity;
            }

            ProductSO product = _idManager.ProductSO(productId);
            int capacity = product == null
                ? SafeFallbackCapacity
                : BoxCapacityHelper.GetSlotCapacity(product);

            if (capacity <= 0)
            {
                capacity = SafeFallbackCapacity;
            }

            SingleSlotCapacityByProduct[productId] = capacity;
            return capacity;
        }
    }
}
