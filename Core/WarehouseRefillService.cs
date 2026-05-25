using System;
using System.Collections.Generic;
using UnityEngine;
using WarehouseRefillPlus.Utilities;
using IL2CPPCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;

namespace WarehouseRefillPlus.Core
{
    public static class WarehouseRefillService
    {
        public static void RefillWarehouse(MarketShoppingCart shoppingCart)
        {
            if (shoppingCart == null) return;

            RackManager rackManager = Object.FindFirstObjectByType<RackManager>();
            IDManager idManager = Object.FindFirstObjectByType<IDManager>();
            SFXManager sfxManager = Object.FindFirstObjectByType<SFXManager>();

            if (rackManager == null || idManager == null)
                return;

            Dictionary<int, int> currentStock = new Dictionary<int, int>();
            Dictionary<int, int> totalCapacity = new Dictionary<int, int>();

            foreach (Rack rack in rackManager.m_Racks)
            {
                if (rack == null)
                    continue;

                for (int i = 0; i < rack.RackSlots.Count; i++)
                {
                    RackSlot rackSlot = rack.RackSlots[i];
                    if (rackSlot == null || rackSlot.Data == null)
                        continue;

                    int productID = rackSlot.Data.ProductID;
                    if (productID <= 0)
                        continue;

                    ProductSO productSO = idManager.ProductSO(productID);
                    if (productSO == null)
                        continue;

                    int boxCount = rackSlot.Data.BoxCount;
                    int slotMax = BoxCapacityHelper.GetSlotCapacity(productSO);

                    // If for some reason current boxCount is higher than our calculated max, adjust max
                    if (boxCount > slotMax) slotMax = boxCount;

                    if (!currentStock.TryAdd(productID, boxCount))
                        currentStock[productID] += boxCount;

                    if (!totalCapacity.TryAdd(productID, slotMax))
                        totalCapacity[productID] += slotMax;
                }
            }

            Dictionary<int, int> amountToOrderPerProduct = new Dictionary<int, int>();
            foreach (var kvp in totalCapacity)
            {
                int productID = kvp.Key;
                int capacity = kvp.Value;
                int stock = currentStock.GetValueOrDefault(productID, 0);

                int limit = WarehouseRefillPlugin.ProductLimits.GetValueOrDefault(productID, capacity);

                int amountToOrder = Math.Max(0, limit - stock);
                if (amountToOrder > 0)
                {
                    amountToOrderPerProduct[productID] = amountToOrder;
                }
            }

            if (amountToOrderPerProduct.Count > 0)
            {
                CartData cartData = shoppingCart.CartData;
                if (cartData != null)
                {
                    foreach (ItemQuantity itemQuantity in cartData.ProductInCarts)
                    {
                        if (itemQuantity == null) continue;

                        int firstItemID = itemQuantity.FirstItemID;
                        if (amountToOrderPerProduct.ContainsKey(firstItemID))
                        {
                            amountToOrderPerProduct[firstItemID] -= itemQuantity.FirstItemCount;
                            if (amountToOrderPerProduct[firstItemID] < 0)
                            {
                                amountToOrderPerProduct[firstItemID] = 0;
                            }
                        }
                    }
                }

                bool addedAny = false;
                foreach (var kvp in amountToOrderPerProduct)
                {
                    if (kvp.Value <= 0)
                        continue;

                    ItemQuantity itemQuantity = new ItemQuantity();
                    IL2CPPCollections.Dictionary<int, int> productsDict = new IL2CPPCollections.Dictionary<int, int>();
                    productsDict.Add(kvp.Key, kvp.Value);
                    itemQuantity.Products = productsDict;
                    shoppingCart.AddProduct(itemQuantity, SalesType.PRODUCT);
                    addedAny = true;
                }

                if (addedAny)
                {
                    shoppingCart.UpdateTotalPrice();
                    if (sfxManager != null)
                    {
                        sfxManager.PlayScanningProductSFX(Vector3.zero);
                    }
                }
            }
        }
    }
}
