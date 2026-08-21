using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using WarehouseRefillPlus.Core;
using WarehouseRefillPlus.UI;

namespace WarehouseRefillPlus.Patches
{
    [HarmonyPatch]
    public static class SalesItemUIPatch
    {
        private static PropertyInfo _cachedPropInfo;
        private static FieldInfo _cachedFieldInfo;
        private static bool _reflectionCached;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            try
            {
                foreach (Type type in typeof(MarketShoppingCart).Assembly.GetTypes())
                {
                    if (type.Name == "SalesItem")
                    {
                        return AccessTools.Method(type, "Start");
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        [HarmonyPostfix]
        public static void Postfix(Component __instance)
        {
            try
            {
                if (__instance == null ||
                    __instance.transform == null ||
                    __instance.gameObject == null ||
                    !__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                if (__instance.transform.Find("SmartLimitButtonGroup") != null)
                {
                    return;
                }

                int productId = -1;
                Type componentType = __instance.GetType();

                if (!_reflectionCached)
                {
                    foreach (PropertyInfo propInfo in componentType.GetProperties())
                    {
                        if (!propInfo.CanRead ||
                            propInfo.GetIndexParameters().Length != 0)
                        {
                            continue;
                        }

                        string propertyName =
                            propInfo.Name.ToLower();

                        if (propertyName is "productid" or "m_productid" or "id" or "itemid" &&
                            propInfo.PropertyType == typeof(int))
                        {
                            _cachedPropInfo = propInfo;
                            break;
                        }
                    }

                    if (_cachedPropInfo == null)
                    {
                        foreach (FieldInfo fieldInfo in componentType.GetFields())
                        {
                            string fieldName =
                                fieldInfo.Name.ToLower();

                            if (fieldName is "productid" or "m_productid" or "id" or "itemid" &&
                                fieldInfo.FieldType == typeof(int))
                            {
                                _cachedFieldInfo = fieldInfo;
                                break;
                            }
                        }
                    }

                    _reflectionCached = true;
                }

                if (_cachedPropInfo != null)
                {
                    productId =
                        (int)_cachedPropInfo.GetValue(__instance)!;
                }
                else if (_cachedFieldInfo != null)
                {
                    productId =
                        (int)_cachedFieldInfo.GetValue(__instance)!;
                }

                if (productId <= 0)
                {
                    return;
                }

                // KEY CHANGE:
                // Do not create MarketAppUIEnhancer at plugin startup or scene load.
                // SalesItem.Start is our Market-open signal. Only now, after Main
                // Scene is already loaded and an actual product card is active,
                // create the enhancer.
                WarehouseRefillPlugin plugin =
                    WarehouseRefillPlugin.Instance;

                if (plugin == null ||
                    !plugin.EnsureMarketUIManagerForOpen(__instance.transform))
                {
                    return;
                }

                int instanceID =
                    __instance.transform.GetInstanceID();

                if (MarketAppUIEnhancer.QueuedParents.Contains(instanceID))
                {
                    return;
                }

                TextMeshProUGUI textMesh =
                    __instance.transform.GetComponentInChildren<TextMeshProUGUI>();

                TMP_FontAsset fontAsset =
                    textMesh != null
                        ? textMesh.font
                        : null;

                MarketAppUIEnhancer.QueuedParents.Add(instanceID);

                MarketAppUIEnhancer.UIQueue.Add(
                    new UIJob
                    {
                        Parent = __instance.transform,
                        ProductId = productId,
                        Font = fontAsset
                    });
            }
            catch
            {
            }
        }
    }
}
