using System;
using System.Collections.Generic;
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
            EnsureSalesItem(__instance);
        }

        internal static bool EnsureSalesItem(Component __instance)
        {
            try
            {
                if (__instance == null ||
                    __instance.transform == null ||
                    __instance.gameObject == null ||
                    !__instance.gameObject.activeInHierarchy)
                {
                    return false;
                }

                Transform parent = __instance.transform;

                if (parent.Find("SmartLimitButtonGroup") != null)
                    return true;

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
                        return false;
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
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                if (productId <= 0)
                    return false;

                WarehouseRefillPlugin plugin =
                    WarehouseRefillPlugin.Instance;

                if (plugin == null)
                    return false;

                if (!plugin.EnsureMarketUIManagerForOpen(parent))
                    return false;

                int worldToken = plugin.CurrentWorldToken;
                if (worldToken == 0)
                    return false;

                int instanceID = parent.GetInstanceID();

                if (MarketAppUIEnhancer.QueuedParents.Contains(instanceID))
                    return true;

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
                        ParentInstanceId = instanceID,
                        ProductId = productId,
                        WorldToken = worldToken,
                        Font = fontAsset
                    });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(ProductViewer), "OnEnable")]
    internal static class ProductViewerUIBootstrapPatch
    {
        private static readonly HashSet<int> BootstrappedViewers = new();

        [HarmonyPostfix]
        private static void Postfix(ProductViewer __instance)
        {
            try
            {
                if (__instance == null ||
                    __instance.gameObject == null ||
                    !__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                int viewerId = __instance.GetInstanceID();
                if (BootstrappedViewers.Contains(viewerId))
                {
                    return;
                }

                Transform content = __instance.ProductsContentParent;
                if (content == null ||
                    content.gameObject == null ||
                    !content.gameObject.activeInHierarchy)
                {
                    return;
                }

                int readyCards = 0;
                for (int i = 0; i < content.childCount; i++)
                {
                    Transform child = content.GetChild(i);
                    if (child == null ||
                        child.gameObject == null ||
                        !child.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    SalesItem salesItem = child.GetComponent<SalesItem>();
                    if (salesItem != null &&
                        SalesItemUIPatch.EnsureSalesItem(salesItem))
                    {
                        readyCards++;
                    }
                }

                if (readyCards == 0)
                {
                    return;
                }

                BootstrappedViewers.Add(viewerId);

                WarehouseRefillPlugin plugin = WarehouseRefillPlugin.Instance;
                if (plugin != null)
                {
                    plugin.Log.LogInfo(
                        $"[WRP] Market UI ready. Existing cards bootstrap={readyCards}");
                }
            }
            catch
            {
            }
        }

        internal static void ResetSceneState()
        {
            BootstrappedViewers.Clear();
        }
    }
}
