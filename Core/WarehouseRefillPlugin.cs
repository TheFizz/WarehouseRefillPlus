using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseRefillPlus.UI;

namespace WarehouseRefillPlus.Core
{
    [BepInPlugin("WarehouseRefillPlus", "Warehouse Refill Plus", "3.2.3")]
    public class WarehouseRefillPlugin : BasePlugin
    {
        public static readonly Dictionary<int, int> ProductLimits = new Dictionary<int, int>();
        public static WarehouseRefillPlugin Instance { get; private set; }

        public static ConfigEntry<bool> EnableAutomaticDelivery { get; private set; }

        // Set this to TRUE only when you want the MAX-label tuning controls
        // to appear in BepInEx Config Manager. FALSE = use the hardcoded values
        // below and do not bind/show those config entries.
        private const bool ShowMaxLabelConfig = false;

        // Final MAX-label values selected in game.
        // 2-column Market view:
        private const float HardcodedMaxLabelTwoColumnOffsetX = 20f;
        private const float HardcodedMaxLabelTwoColumnOffsetY = -1.5f;
        private const float HardcodedMaxLabelTwoColumnTextScale = 1.00f;

        // 3-column Market view:
        private const float HardcodedMaxLabelThreeColumnOffsetX = 12f;
        private const float HardcodedMaxLabelThreeColumnOffsetY = 3f;
        private const float HardcodedMaxLabelThreeColumnTextScale = 0.85f;

        public static ConfigEntry<float> MaxLabelTwoColumnOffsetX { get; private set; }
        public static ConfigEntry<float> MaxLabelTwoColumnOffsetY { get; private set; }
        public static ConfigEntry<float> MaxLabelTwoColumnTextScale { get; private set; }

        public static ConfigEntry<float> MaxLabelThreeColumnOffsetX { get; private set; }
        public static ConfigEntry<float> MaxLabelThreeColumnOffsetY { get; private set; }
        public static ConfigEntry<float> MaxLabelThreeColumnTextScale { get; private set; }

        public static bool AutomaticDeliveryEnabled =>
            EnableAutomaticDelivery?.Value ?? true;

        public static float MaxLabelTwoColumnOffsetXValue =>
            ShowMaxLabelConfig && MaxLabelTwoColumnOffsetX != null
                ? MaxLabelTwoColumnOffsetX.Value
                : HardcodedMaxLabelTwoColumnOffsetX;

        public static float MaxLabelTwoColumnOffsetYValue =>
            ShowMaxLabelConfig && MaxLabelTwoColumnOffsetY != null
                ? MaxLabelTwoColumnOffsetY.Value
                : HardcodedMaxLabelTwoColumnOffsetY;

        public static float MaxLabelTwoColumnTextScaleValue =>
            ShowMaxLabelConfig && MaxLabelTwoColumnTextScale != null
                ? MaxLabelTwoColumnTextScale.Value
                : HardcodedMaxLabelTwoColumnTextScale;

        public static float MaxLabelThreeColumnOffsetXValue =>
            ShowMaxLabelConfig && MaxLabelThreeColumnOffsetX != null
                ? MaxLabelThreeColumnOffsetX.Value
                : HardcodedMaxLabelThreeColumnOffsetX;

        public static float MaxLabelThreeColumnOffsetYValue =>
            ShowMaxLabelConfig && MaxLabelThreeColumnOffsetY != null
                ? MaxLabelThreeColumnOffsetY.Value
                : HardcodedMaxLabelThreeColumnOffsetY;

        public static float MaxLabelThreeColumnTextScaleValue =>
            ShowMaxLabelConfig && MaxLabelThreeColumnTextScale != null
                ? MaxLabelThreeColumnTextScale.Value
                : HardcodedMaxLabelThreeColumnTextScale;

        public static string LimitFilePath =>
            Path.Combine(Application.persistentDataPath, "SmartCartLimits.txt");

        private const string ManagerDebugBuild = "2026-08-21-D11-HARDCODED-MAX";

        private GameObject _uiManager;
        private bool _gameplaySceneReady;

        public override void Load()
        {
            Instance = this;

            EnableAutomaticDelivery = Config.Bind(
                "Automatic Delivery",
                "Enabled",
                true,
                "Automatically move newly delivered product boxes to matching warehouse racks. " +
                "Disable this option to leave deliveries in the normal delivery area. " +
                "The F10 manual rack shortcut remains available.");

            if (ShowMaxLabelConfig)
            {
                MaxLabelTwoColumnOffsetX = Config.Bind(
                    "MAX Label - 2 Columns",
                    "Offset X",
                    HardcodedMaxLabelTwoColumnOffsetX,
                    new ConfigDescription(
                        "Horizontal MAX label position in the 2-column Market view. " +
                        "Higher values move the text right; lower values move it left.",
                        new AcceptableValueRange<float>(-100f, 100f)));

                MaxLabelTwoColumnOffsetY = Config.Bind(
                    "MAX Label - 2 Columns",
                    "Offset Y",
                    HardcodedMaxLabelTwoColumnOffsetY,
                    new ConfigDescription(
                        "Vertical MAX label position in the 2-column Market view. " +
                        "Higher values move the text up; lower values move it down.",
                        new AcceptableValueRange<float>(-50f, 50f)));

                MaxLabelTwoColumnTextScale = Config.Bind(
                    "MAX Label - 2 Columns",
                    "Text Scale",
                    HardcodedMaxLabelTwoColumnTextScale,
                    new ConfigDescription(
                        "MAX text size multiplier in the 2-column Market view. " +
                        "1.00 means the same font size as the native stock-count text.",
                        new AcceptableValueRange<float>(0.30f, 2.00f)));

                MaxLabelThreeColumnOffsetX = Config.Bind(
                    "MAX Label - 3 Columns",
                    "Offset X",
                    HardcodedMaxLabelThreeColumnOffsetX,
                    new ConfigDescription(
                        "Horizontal MAX label position in the 3-column Market view. " +
                        "Higher values move the text right; lower values move it left.",
                        new AcceptableValueRange<float>(-100f, 100f)));

                MaxLabelThreeColumnOffsetY = Config.Bind(
                    "MAX Label - 3 Columns",
                    "Offset Y",
                    -1.5f,
                    new ConfigDescription(
                        "Vertical MAX label position in the 3-column Market view. " +
                        "Higher values move the text up; lower values move it down.",
                        new AcceptableValueRange<float>(-50f, 50f)));

                MaxLabelThreeColumnTextScale = Config.Bind(
                    "MAX Label - 3 Columns",
                    "Text Scale",
                    0.72f,
                    new ConfigDescription(
                        "MAX text size multiplier in the 3-column Market view. " +
                        "1.00 means the same font size as the native stock-count text.",
                        new AcceptableValueRange<float>(0.30f, 2.00f)));

            }

            LoadLimits();

            // Register the IL2CPP type now, but DO NOT create the component here.
            // Creating MarketAppUIEnhancer during plugin startup is too early:
            // SignIn/Main Menu do not yet have the final store computer/Market UI.
            ClassInjector.RegisterTypeInIl2Cpp<MarketAppUIEnhancer>();

            SceneManager.add_sceneLoaded(
                new Action<Scene, LoadSceneMode>(OnSceneLoaded));

            Harmony harmony =
                new Harmony("WarehouseRefillPlus.patch");
            harmony.PatchAll();

            Log.LogInfo(
                $"Load successful! Automatic delivery: {AutomaticDeliveryEnabled}");

            Log.LogInfo(
                $"[MAXDBG] MANAGER BUILD {ManagerDebugBuild} armed. " +
                "Waiting for Main Scene and the first active Market SalesItem.");
        }

        private void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // The real gameplay scene is loaded as Single in the current game.
            // Additive helper scenes (for example SignIn) must not arm the Market UI.
            if (mode != LoadSceneMode.Single)
            {
                return;
            }

            // Any jobs/references from the previous scene are invalid now.
            MarketAppUIEnhancer.UIQueue.Clear();
            MarketAppUIEnhancer.QueuedParents.Clear();

            // The manager is deliberately NOT persistent anymore.
            // A manager created in the previous scene is destroyed by Unity with
            // that scene; clear our managed reference and wait for Market to open.
            _uiManager = null;

            _gameplaySceneReady =
                string.Equals(
                    scene.name,
                    "Main Scene",
                    StringComparison.Ordinal);

            if (_gameplaySceneReady)
            {
                Log.LogInfo(
                    $"[MAXDBG] SCENE READY name='{scene.name}' " +
                    $"buildIndex={scene.buildIndex}. Waiting for Market SalesItem.Start.");
            }
            else
            {
                Log.LogInfo(
                    $"[MAXDBG] SCENE WAIT name='{scene.name}' " +
                    $"buildIndex={scene.buildIndex}. Market manager not created.");
            }
        }

        /// <summary>
        /// Called by SalesItemUIPatch only when a real SalesItem becomes active.
        /// In practice this happens when the player opens Market and its product
        /// cards are enabled. This is the first point where we create the enhancer.
        /// </summary>
        public bool EnsureMarketUIManagerForOpen(
            Transform salesItemTransform)
        {
            if (!_gameplaySceneReady)
            {
                return false;
            }

            if (salesItemTransform == null ||
                salesItemTransform.gameObject == null ||
                !salesItemTransform.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (_uiManager == null)
            {
                GameObject existing =
                    GameObject.Find("WarehouseRefillPlus_UIManager");

                _uiManager = existing != null
                    ? existing
                    : new GameObject("WarehouseRefillPlus_UIManager");

                // Intentionally no DontDestroyOnLoad.
                // The manager belongs to the gameplay scene and is recreated only
                // after Market opens in the next gameplay scene.
            }

            MarketAppUIEnhancer enhancer =
                _uiManager.GetComponent<MarketAppUIEnhancer>();

            if (enhancer == null)
            {
                enhancer =
                    _uiManager.AddComponent<MarketAppUIEnhancer>();

                Log.LogInfo(
                    $"[MAXDBG] MARKET OPEN -> enhancer created. " +
                    $"trigger='{GetTransformPath(salesItemTransform)}' " +
                    $"scene='{SceneManager.GetActiveScene().name}' " +
                    $"managerId={_uiManager.GetInstanceID()} " +
                    $"enhancerId={enhancer.GetInstanceID()}");
            }

            if (!_uiManager.activeSelf)
            {
                _uiManager.SetActive(true);
            }

            return enhancer != null;
        }

        private static string GetTransformPath(
            Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            Transform current = transform.parent;

            int depth = 0;
            while (current != null && depth < 8)
            {
                path = current.name + "/" + path;
                current = current.parent;
                depth++;
            }

            return path;
        }

        public static void SaveLimit(
            int id,
            int amount)
        {
            ProductLimits[id] = amount;

            List<string> lines =
                new List<string>();

            foreach (KeyValuePair<int, int> kvp
                     in ProductLimits)
            {
                lines.Add(
                    $"{kvp.Key}:{kvp.Value}");
            }

            try
            {
                File.WriteAllLines(
                    LimitFilePath,
                    lines);
            }
            catch
            {
            }
        }

        private void LoadLimits()
        {
            if (!File.Exists(LimitFilePath))
            {
                return;
            }

            try
            {
                foreach (string line
                         in File.ReadAllLines(LimitFilePath))
                {
                    string[] parts =
                        line.Split(':');

                    if (parts.Length == 2 &&
                        int.TryParse(
                            parts[0],
                            out int productId) &&
                        int.TryParse(
                            parts[1],
                            out int limitAmount))
                    {
                        ProductLimits[productId] =
                            limitAmount;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
