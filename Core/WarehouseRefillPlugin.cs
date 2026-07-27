using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseRefillPlus.UI;

namespace WarehouseRefillPlus.Core
{
    [BepInPlugin("WarehouseRefillPlus", "Warehouse Refill Plus", "3.2.0")]
    public class WarehouseRefillPlugin : BasePlugin
    {
        public static readonly Dictionary<int, int> ProductLimits = new Dictionary<int, int>();
        public static WarehouseRefillPlugin Instance { get; private set; }

        public static string LimitFilePath => Path.Combine(Application.persistentDataPath, "SmartCartLimits.txt");

        public override void Load()
        {
            Instance = this;
            LoadLimits();
            ClassInjector.RegisterTypeInIl2Cpp<MarketAppUIEnhancer>();
            SceneManager.add_sceneLoaded(new Action<Scene, LoadSceneMode>(OnSceneLoaded));
            Harmony harmony = new Harmony("WarehouseRefillPlus.patch");
            harmony.PatchAll();
            Log.LogInfo($"Load successful!");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MarketAppUIEnhancer.UIQueue.Clear();
            MarketAppUIEnhancer.QueuedParents.Clear();
            if (scene.buildIndex != 0)
            {
                if (GameObject.Find("WarehouseRefillPlus_UIManager") == null)
                {
                    GameObject uiManager = new GameObject("WarehouseRefillPlus_UIManager");
                    uiManager.AddComponent<MarketAppUIEnhancer>();
                }
            }
        }

        public static void SaveLimit(int id, int amount)
        {
            ProductLimits[id] = amount;
            List<string> lines = new List<string>();
            foreach (KeyValuePair<int, int> kvp in ProductLimits)
            {
                lines.Add($"{kvp.Key}:{kvp.Value}");
            }

            try
            {
                File.WriteAllLines(LimitFilePath, lines);
            }
            catch
            {
            }
        }

        private void LoadLimits()
        {
            if (File.Exists(LimitFilePath))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(LimitFilePath))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int productId) && int.TryParse(parts[1], out int limitAmount))
                        {
                            ProductLimits[productId] = limitAmount;
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
