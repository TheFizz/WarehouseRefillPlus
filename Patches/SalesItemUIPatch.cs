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
                Assembly assembly = typeof(MarketShoppingCart).Assembly;
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some IL2CPP/BepInEx environments may fail to load
                    // a small number of generated types.
                    // Keep all successfully loaded types instead of
                    // abandoning the SalesItem patch completely.
                    types = ex.Types;
                }

                if (types == null)
                    return null;

                foreach (Type type in types)
                {
                    if (type == null)
                        continue;

                    try
                    {
                        if (string.Equals(
                                type.Name,
                                "SalesItem",
                                StringComparison.Ordinal))
                        {
                            return AccessTools.Method(type, "Start");
                        }
                    }
                    catch
                    {
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

                Transform parent = __instance.transform;

                if (parent.Find("SmartLimitButtonGroup") != null)
                    return;

                Type componentType = __instance.GetType();

                if (!_reflectionCached)
                {
                    try
                    {
                        PropertyInfo[] properties =
                            componentType.GetProperties();

                        foreach (PropertyInfo propInfo in properties)
                        {
                            if (propInfo == null ||
                                !propInfo.CanRead ||
                                propInfo.GetIndexParameters().Length != 0)
                            {
                                continue;
                            }

                            string name =
                                propInfo.Name.ToLowerInvariant();

                            if ((name == "productid" ||
                                 name == "m_productid" ||
                                 name == "id" ||
                                 name == "itemid") &&
                                propInfo.PropertyType == typeof(int))
                            {
                                _cachedPropInfo = propInfo;
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (_cachedPropInfo == null)
                    {
                        try
                        {
                            FieldInfo[] fields =
                                componentType.GetFields();

                            foreach (FieldInfo fieldInfo in fields)
                            {
                                if (fieldInfo == null)
                                    continue;

                                string name =
                                    fieldInfo.Name.ToLowerInvariant();

                                if ((name == "productid" ||
                                     name == "m_productid" ||
                                     name == "id" ||
                                     name == "itemid") &&
                                    fieldInfo.FieldType == typeof(int))
                                {
                                    _cachedFieldInfo = fieldInfo;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    _reflectionCached = true;
                }

                int productId = -1;

                if (_cachedPropInfo != null)
                {
                    try
                    {
                        object value =
                            _cachedPropInfo.GetValue(__instance);

                        if (value != null)
                            productId = (int)value;
                    }
                    catch
                    {
                        return;
                    }
                }
                else if (_cachedFieldInfo != null)
                {
                    try
                    {
                        object value =
                            _cachedFieldInfo.GetValue(__instance);

                        if (value != null)
                            productId = (int)value;
                    }
                    catch
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }

                if (productId <= 0)
                    return;

                WarehouseRefillPlugin plugin =
                    WarehouseRefillPlugin.Instance;

                if (plugin == null)
                    return;

                if (!plugin.EnsureMarketUIManagerForOpen(parent))
                    return;

                int instanceID = parent.GetInstanceID();

                if (MarketAppUIEnhancer.QueuedParents.Contains(instanceID))
                    return;

                TMP_FontAsset fontAsset = null;

                try
                {
                    TextMeshProUGUI textMesh =
                        parent.GetComponentInChildren<TextMeshProUGUI>();

                    if (textMesh != null)
                        fontAsset = textMesh.font;
                }
                catch
                {
                }

                MarketAppUIEnhancer.QueuedParents.Add(instanceID);

                MarketAppUIEnhancer.UIQueue.Add(
                    new UIJob
                    {
                        Parent = parent,
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