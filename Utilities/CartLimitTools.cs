using System;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using WarehouseRefillPlus.Core;

namespace WarehouseRefillPlus.Utilities
{
	public static class MarketShoppingCartLimitTools
	{
		public static void Apply(MarketShoppingCart cart)
		{
			if (cart == null)
			{
				return;
			}
			try
			{
				if (cart.m_MaxItemCount < 9999)
				{
					cart.m_MaxItemCount = 9999;
				}
				if (cart.m_CartMaxedIndicator != null && cart.m_CartMaxedIndicator.activeSelf)
				{
					cart.m_CartMaxedIndicator.SetActive(false);
				}
			}
			catch (Exception ex)
			{
				WarehouseRefillPlugin.Instance?.Log?.LogWarning($"[WarehouseRefill] Apply cart limit failed: {ex.Message}");
			}
		}

		public const int NewMaxCartItems = 9999;
	}
}

