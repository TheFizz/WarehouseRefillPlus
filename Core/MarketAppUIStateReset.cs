using System.Collections;
using System.Reflection;
using UnityEngine;
using WarehouseRefillPlus.UI;

namespace WarehouseRefillPlus.Core
{
    /// <summary>
    /// Clears references owned by the previous gameplay scene while keeping the
    /// persistent MarketAppUIEnhancer component alive.
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

        public static void Reset(
            MarketAppUIEnhancer enhancer)
        {
            if (enhancer == null)
            {
                return;
            }

            MarketAppUIEnhancer.UIQueue.Clear();
            MarketAppUIEnhancer.QueuedParents.Clear();

            SetInstanceField(CheckTimerField, enhancer, 0f);
            SetInstanceField(CartField, enhancer, null);
            SetInstanceField(ComputerField, enhancer, null);
            SetInstanceField(MarketContentField, enhancer, null);
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

            // Sprites and their runtime textures can become invalid after the old
            // scene is unloaded. A cleared cache forces both embedded icons to be
            // loaded again for the new computer UI.
            ClearCollection(
                SpriteCacheField,
                null);

            ResetGlobalInput();
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
            }
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
