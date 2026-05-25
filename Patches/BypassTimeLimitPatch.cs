using HarmonyLib;

namespace WarehouseRefillPlus.Patches
{
	[HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.TooLateToOrderGoods), MethodType.Getter)]
	public static class BypassTimeLimitPatch
	{
		public static bool Prefix(ref bool __result)
		{
			__result = false;
			return false;
		}
	}
}

