using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using WarehouseRefillPlus.UI;

namespace WarehouseRefillPlus.Core
{
    /// <summary>
    /// Clears references and processed UI state owned by the previous gameplay
    /// scene. Persistent product limits are intentionally not touched.
    /// </summary>
    internal static class MarketAppUIStateReset
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic;

        private const BindingFlags StaticFlags =
            BindingFlags.Static |
            BindingFlags.NonPublic;

        private static readonly FieldInfo CheckTimerField =
            typeof(MarketAppUIEnhancer).GetField(
                "_checkTimer",
                InstanceFlags);

        private static readonly FieldInfo MarketOpenReadyTimerField =
            typeof(MarketAppUIEnhancer).GetField(
                "_marketOpenReadyTimer",
                InstanceFlags);

        private static readonly FieldInfo QueueReadyLoggedField =
            typeof(MarketAppUIEnhancer).GetField(
                "_queueReadyLogged",
                InstanceFlags);

        private static readonly FieldInfo MaxDebugStartupLoggedField =
            typeof(MarketAppUIEnhancer).GetField(
                "_maxDebugStartupLogged",
                InstanceFlags);

        private static readonly FieldInfo CartField =
            typeof(MarketAppUIEnhancer).GetField(
                "_cart",
                InstanceFlags);

        private static readonly FieldInfo ComputerField =
            typeof(MarketAppUIEnhancer).GetField(
                "_computer",
                InstanceFlags);

        private static readonly FieldInfo MarketContentField =
            typeof(MarketAppUIEnhancer).GetField(
                "_marketContentCache",
                InstanceFlags);

        private static readonly FieldInfo MarketRootField =
            typeof(MarketAppUIEnhancer).GetField(
                "_marketRootCache",
                InstanceFlags);

        private static readonly FieldInfo BuyingPanelField =
            typeof(MarketAppUIEnhancer).GetField(
                "_buyingPanelCache",
                InstanceFlags);

        private static readonly FieldInfo PurchaseButtonField =
            typeof(MarketAppUIEnhancer).GetField(
                "_purchaseButtonCache",
                InstanceFlags);

        private static readonly FieldInfo TaskbarField =
            typeof(MarketAppUIEnhancer).GetField(
                "_taskbarTransformCache",
                InstanceFlags);

        private static readonly FieldInfo CartButtonField =
            typeof(MarketAppUIEnhancer).GetField(
                "_cartButtonTransformCache",
                InstanceFlags);

        private static readonly FieldInfo OriginalPositionsField =
            typeof(MarketAppUIEnhancer).GetField(
                "_originalPositions",
                InstanceFlags);

        private static readonly FieldInfo SmartLimitGroupsField =
            typeof(MarketAppUIEnhancer).GetField(
                "_smartLimitGroups",
                InstanceFlags);

        private static readonly FieldInfo SpriteCacheField =
            typeof(MarketAppUIEnhancer).GetField(
                "SpriteCache",
                StaticFlags);

        private static readonly FieldInfo GlobalInputObjectField =
            typeof(MarketAppUIEnhancer).GetField(
                "_globalInputObj",
                StaticFlags);

        private static readonly FieldInfo GlobalInputField =
            typeof(MarketAppUIEnhancer).GetField(
                "_globalInput",
                StaticFlags);

        private static readonly FieldInfo EditingTextField =
            typeof(MarketAppUIEnhancer).GetField(
                "_editingText",
                StaticFlags);

        private static readonly FieldInfo EditingProductIdField =
            typeof(MarketAppUIEnhancer).GetField(
                "_editingProductId",
                StaticFlags);

        private static readonly FieldInfo MaxDebugLastStateField =
            typeof(MarketAppUIEnhancer).GetField(
                "MaxDebugLastState",
                StaticFlags);

        private static readonly FieldInfo MaxDebugExpectedLocalField =
            typeof(MarketAppUIEnhancer).GetField(
                "MaxDebugExpectedLocal",
                StaticFlags);

        private static readonly FieldInfo MaxGroupsWithValidAnchorField =
            typeof(MarketAppUIEnhancer).GetField(
                "MaxGroupsWithValidAnchor",
                StaticFlags);

        public static void Reset(
            MarketAppUIEnhancer enhancer)
        {
            MarketAppUIEnhancer.UIQueue.Clear();
            MarketAppUIEnhancer.QueuedParents.Clear();

            ClearCollection(SpriteCacheField, null);
            ClearCollection(MaxDebugLastStateField, null);
            ClearCollection(MaxDebugExpectedLocalField, null);
            ClearCollection(MaxGroupsWithValidAnchorField, null);
            ResetGlobalInput();

            if (enhancer == null)
            {
                return;
            }

            SetInstanceField(CheckTimerField, enhancer, 0f);
            SetInstanceField(MarketOpenReadyTimerField, enhancer, 0f);
            SetInstanceField(QueueReadyLoggedField, enhancer, false);
            SetInstanceField(MaxDebugStartupLoggedField, enhancer, false);
            SetInstanceField(CartField, enhancer, null);
            SetInstanceField(ComputerField, enhancer, null);
            SetInstanceField(MarketContentField, enhancer, null);
            SetInstanceField(MarketRootField, enhancer, null);
            SetInstanceField(BuyingPanelField, enhancer, null);
            SetInstanceField(PurchaseButtonField, enhancer, null);
            SetInstanceField(TaskbarField, enhancer, null);
            SetInstanceField(CartButtonField, enhancer, null);

            ClearCollection(
                OriginalPositionsField,
                enhancer);

            ClearCollection(
                SmartLimitGroupsField,
                enhancer);

        }

        private static void ResetGlobalInput()
        {
            GameObject globalInputObject =
                GlobalInputObjectField?.GetValue(null)
                    as GameObject;

            if (globalInputObject != null)
            {
                UnityEngine.Object.Destroy(
                    globalInputObject);
            }

            SetStaticField(
                GlobalInputObjectField,
                null);

            SetStaticField(
                GlobalInputField,
                null);

            SetStaticField(
                EditingTextField,
                null);

            SetStaticField(
                EditingProductIdField,
                -1);
        }

        private static void ClearCollection(
            FieldInfo field,
            object target)
        {
            object collection = field?.GetValue(target);
            if (collection is IDictionary dictionary)
            {
                dictionary.Clear();
                return;
            }

            if (collection is IList list)
            {
                list.Clear();
                return;
            }

            collection?.GetType()
                .GetMethod("Clear", Type.EmptyTypes)
                ?.Invoke(collection, null);
        }

        private static void SetInstanceField(
            FieldInfo field,
            object target,
            object value)
        {
            field?.SetValue(target, value);
        }

        private static void SetStaticField(
            FieldInfo field,
            object value)
        {
            field?.SetValue(null, value);
        }
    }
}
