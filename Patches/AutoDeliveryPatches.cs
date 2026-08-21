using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using WarehouseRefillPlus.Core;

namespace WarehouseRefillPlus.Patches
{
    [HarmonyPatch]
    public static class AutoDeliveryPatches
    {
        // Tymczasowa lista do przechwycenia obiektów z dostawy
        private static readonly List<GameObject> _lastDeliveredBoxes = new List<GameObject>();

        // 1. Przechwytujemy moment dostawy kurierskiej
        [HarmonyPatch(typeof(SortableBoxManager), "OnDeliveryCompleted")]
        [HarmonyPrefix]
        public static void OnDeliveryCompleted_Prefix(ref Il2CppSystem.Collections.Generic.List<GameObject> products)
        {
            _lastDeliveredBoxes.Clear();

            // Gdy opcja jest wyłączona, nie kopiujemy nawet listy dostarczonych pudeł.
            if (!WarehouseRefillPlugin.AutomaticDeliveryEnabled)
            {
                return;
            }

            if (products != null)
            {
                // Konwersja z listy IL2CPP na standardową listę C#
                foreach (var obj in products)
                {
                    _lastDeliveredBoxes.Add(obj);
                }
            }
        }

        [HarmonyPatch(typeof(SortableBoxManager), "OnDeliveryCompleted")]
        [HarmonyPostfix]
        public static void OnDeliveryCompleted_Postfix()
        {
            if (!WarehouseRefillPlugin.AutomaticDeliveryEnabled)
            {
                _lastDeliveredBoxes.Clear();
                return;
            }

            if (_lastDeliveredBoxes.Count > 0)
            {
                // Wysyłamy pudła do naszego menedżera układania
                AutoDeliveryService.ProcessDeliveredBoxes(_lastDeliveredBoxes);
                _lastDeliveredBoxes.Clear();
            }
        }

        // 2. Skrót F10 pozostaje dostępny niezależnie od ustawienia automatycznej dostawy.
        [HarmonyPatch(typeof(PlayerInteraction), "Update")]
        [HarmonyPostfix]
        public static void PlayerInteraction_Update_Postfix(PlayerInteraction __instance)
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                AutoDeliveryService.TryStoreHeldBox();
            }
        }
    }
}
