using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WarehouseRefillPlus.Core
{
    public static class AutoDeliveryService
    {
        // Docelowa rotacja pudełka po zakończeniu natywnej animacji RackSlot.AddBox.
        // Klucz = Unity InstanceID pudełka.
        private static readonly Dictionary<int, Quaternion> _pendingFinalRackRotations =
            new Dictionary<int, Quaternion>();

        private static readonly FieldInfo _boxSoField =
            AccessTools.Field(typeof(Box), "m_BoxSO");

        /// <summary>
        /// RackSlot.AddBox animuje rotację przez DOTween, a na końcu wywołuje
        /// Box.ToggleInstanced(true). W tym dokładnym momencie wymuszamy jeszcze raz
        /// rotację wynikającą z BoxSO.GridLayout.boxAngle, zanim batching/instancing
        /// zapamięta transform pudełka.
        /// </summary>
        public static void ApplyPendingFinalRackRotation(Box box)
        {
            if (box == null)
                return;

            int id = box.GetInstanceID();
            if (!_pendingFinalRackRotations.TryGetValue(id, out Quaternion targetRotation))
                return;

            _pendingFinalRackRotations.Remove(id);

            try
            {
                Vector3 beforeEuler = box.transform.localEulerAngles;
                box.transform.localRotation = targetRotation;

                Rigidbody rb = box.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                WarehouseRefillPlugin.Instance.Log.LogInfo(
                    $"[AUTO-RACK ROT] box={box.BoxID} product={box.Data?.ProductID ?? 0} " +
                    $"before=({beforeEuler.x:F1},{beforeEuler.y:F1},{beforeEuler.z:F1}) " +
                    $"final=({box.transform.localEulerAngles.x:F1}," +
                    $"{box.transform.localEulerAngles.y:F1}," +
                    $"{box.transform.localEulerAngles.z:F1})");
            }
            catch (System.Exception ex)
            {
                WarehouseRefillPlugin.Instance.Log.LogWarning(
                    $"[AUTO-RACK ROT] Nie udało się ustawić końcowej rotacji: {ex.Message}");
            }
        }

        private static Quaternion GetNativeRackRotation(Box box)
        {
            try
            {
                BoxSO boxSo = _boxSoField?.GetValue(box) as BoxSO;
                if (boxSo != null && boxSo.GridLayout != null)
                {
                    Vector3 angle = boxSo.GridLayout.boxAngle;

                    // Kartony były już ustawione równo, ale tyłem do przodu.
                    // Obracamy je o 180° wokół osi Y, zachowując natywny kąt layoutu.
                    angle.y += 180f;

                    return Quaternion.Euler(angle);
                }
            }
            catch
            {
                // Fallback poniżej.
            }

            return Quaternion.Euler(0f, 180f, 0f);
        }

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

                        // Odtwarzamy kolejność używaną przez natywne BoxInteraction.PlaceBoxToRack().
                        // Racked musi być ustawione PRZED RackSlot.AddBox().
                        box.Racked = true;

                        // Natywna ścieżka zwalnia również ewentualne zajęcie pudełka.
                        try { box.SetOccupy(false, null); } catch { }

                        // Zapisujemy DOKŁADNĄ rotację, której używa RackSlot.AddBox:
                        // BoxSO.GridLayout.boxAngle. Nie prostujemy pudełka za wcześnie,
                        // bo AddBox uruchamia własny tween rotacji.
                        int instanceId = box.GetInstanceID();
                        _pendingFinalRackRotations[instanceId] = GetNativeRackRotation(box);

                        try
                        {
                            slot.AddBox(box.BoxID, box, true);
                        }
                        catch
                        {
                            _pendingFinalRackRotations.Remove(instanceId);
                            throw;
                        }

                        // Nie wywołujemy SetStatic(true).
                        // Końcowa korekta zostanie wykonana w Prefixie Box.ToggleInstanced(true),
                        // czyli dokładnie po zakończeniu animacji AddBox i PRZED zapisaniem
                        // transformu do renderowania instancjonowanego.

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
