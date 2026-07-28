using System.Collections.Generic;
using UnityEngine;

namespace WarehouseRefillPlus.Core
{
    public static class AutoDeliveryService
    {
        // Obsługa automatycznej dostawy (pudła na ulicy)
        public static void ProcessDeliveredBoxes(List<GameObject> boxObjects)
        {
            RackManager rackManager = Object.FindFirstObjectByType<RackManager>();
            if (rackManager == null) return;

            foreach (GameObject boxObj in boxObjects)
            {
                if (boxObj == null) continue;
                Box box = boxObj.GetComponent<Box>();

                if (box == null || box.Data == null || box.Data.ProductID <= 0 || box.GetComponent<FurnitureBox>())
                    continue;

                // Używamy cichego dodawania (bez udziału rąk gracza)
                TryStoreBoxSilently(box, rackManager);
            }
        }

        // Obsługa wciskania klawisza (pudło w dłoniach)
        public static void TryStoreHeldBox()
        {
            PlayerManager playerManager = Object.FindFirstObjectByType<PlayerManager>();
            RackManager rackManager = Object.FindFirstObjectByType<RackManager>();

            if (playerManager == null || rackManager == null) return;
            BoxInteraction boxInteraction = playerManager.LocalPlayer.BoxInteraction;

            Box heldBox = boxInteraction.m_Box;
            if (heldBox != null && heldBox.Data != null && heldBox.Data.ProductID > 0 && !heldBox.GetComponent<FurnitureBox>())
            {
                // Tutaj używamy natywnej funkcji, bo gracz FIZYCZNIE trzyma pudełko
                TryStoreBoxFromHands(heldBox, rackManager, boxInteraction);
            }
        }

        // --- METODA 1: Natywne odkładanie bez udziału gracza (Dla dostaw) ---
        private static bool TryStoreBoxSilently(Box box, RackManager rackManager)
        {
            foreach (Rack rack in rackManager.m_Racks)
            {
                if (rack == null) continue;

                foreach (RackSlot slot in rack.RackSlots)
                {
                    if (slot == null ||
                        slot.Data == null ||
                        slot.Data.ProductID != box.Data.ProductID ||
                        slot.Full)
                    {
                        continue;
                    }

                    try
                    {
                        // Zatrzymujemy fizykę przed przekazaniem pudełka do RackSlot,
                        // ale NIE zmieniamy rodzica, pozycji ani rotacji ręcznie.
                        Rigidbody rb = box.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                            rb.isKinematic = true;
                        }

                        // RackSlot.AddBox sam ustawia właściwy parent, localPosition,
                        // localRotation oraz układ pudełka w slocie.
                        slot.AddBox(box.BoxID, box, true);

                        // Flagi ustawiamy dopiero po natywnym dodaniu do regału.
                        box.Racked = true;
                        try { box.SetStatic(true); } catch { }

                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        WarehouseRefillPlugin.Instance.Log.LogError(
                            $"Błąd cichego układania: {ex.Message}");
                        return false;
                    }
                }
            }

            return false;
        }

        // --- METODA 2: Odkładanie z rąk (Dla klawisza F10) ---
        private static bool TryStoreBoxFromHands(Box box, RackManager rackManager, BoxInteraction boxInteraction)
        {
            foreach (Rack rack in rackManager.m_Racks)
            {
                if (rack == null) continue;
                foreach (RackSlot slot in rack.RackSlots)
                {
                    if (slot != null && slot.Data != null && slot.Data.ProductID == box.Data.ProductID)
                    {
                        if (!slot.Full)
                        {
                            // Uruchamiamy oryginalną animację i dźwięk odkładania
                            boxInteraction.m_CurrentRackSlot = slot;
                            boxInteraction.PlaceBoxToRack();
                            boxInteraction.m_CurrentRackSlot = null;
                            return true;
                        }
                    }
                }
            }
            return false; // Nie znaleziono miejsca
        }
    }
}
