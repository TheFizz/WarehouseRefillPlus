using HarmonyLib;
using WarehouseRefillPlus.Utilities;

namespace WarehouseRefillPlus.Patches
{
    [HarmonyPatch(typeof(MarketShoppingCart))]
    public static class MarketShoppingCartPatches
    {
        [HarmonyPatch(nameof(MarketShoppingCart.AddProduct))]
        [HarmonyPrefix]
        public static void AddProduct_Prefix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.TryAddProduct))]
        [HarmonyPrefix]
        public static void TryAddProduct_Prefix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.OnItemCountChangedByButtons))]
        [HarmonyPrefix]
        public static void OnItemCountChangedByButtons_Prefix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.Awake))]
        [HarmonyPostfix]
        public static void Awake_Postfix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.Start))]
        [HarmonyPostfix]
        public static void Start_Postfix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.Initialize))]
        [HarmonyPostfix]
        public static void Initialize_Postfix(MarketShoppingCart __instance)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
        }

        [HarmonyPatch(nameof(MarketShoppingCart.CartMaxed))]
        [HarmonyPrefix]
        public static bool CartMaxed_Prefix(MarketShoppingCart __instance, ref bool __result)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
            __result = false;
            return false;
        }

        [HarmonyPatch(nameof(MarketShoppingCart.CartMaxedPassive))]
        [HarmonyPrefix]
        public static bool CartMaxedPassive_Prefix(MarketShoppingCart __instance, ref bool __result)
        {
            MarketShoppingCartLimitTools.Apply(__instance);
            __result = false;
            return false;
        }
    }
}

