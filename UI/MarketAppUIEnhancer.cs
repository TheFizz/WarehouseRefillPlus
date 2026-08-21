using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using __Project__.Scripts.Computer;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseRefillPlus.Core;
using WarehouseRefillPlus.Utilities;

namespace WarehouseRefillPlus.UI
{
    public class MarketAppUIEnhancer : MonoBehaviour
    {
        public MarketAppUIEnhancer(IntPtr ptr)
            : base(ptr)
        {
        }

        public static readonly List<UIJob> UIQueue = new List<UIJob>();
        public static readonly HashSet<int> QueuedParents = new HashSet<int>();
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        private float _checkTimer;
        private float _marketOpenReadyTimer;
        private bool _queueReadyLogged;
        private MarketShoppingCart _cart;
        private Computer _computer;
        private Transform _marketContentCache;
        private Transform _buyingPanelCache;
        private Transform _purchaseButtonCache;
        private Transform _taskbarTransformCache;
        private Transform _cartButtonTransformCache;

        private static int _editingProductId = -1;
        private static GameObject _globalInputObj;
        private static TMP_InputField _globalInput;
        private static TextMeshProUGUI _editingText;
        private readonly Dictionary<string, Vector3> _originalPositions = new();
        private readonly List<RectTransform> _smartLimitGroups = new();

        // MAX label position and font scale come from WarehouseRefillPlugin.
        // By default those values are hardcoded. If ShowMaxLabelConfig is changed
        // to true in WarehouseRefillPlugin, the same properties read ConfigEntry
        // values live instead.
        private const float MaxFontAbsoluteMin = 6f;
        private const float MaxFontAbsoluteMax = 30f;

        private const string MaxDebugBuild = "2026-08-21-D13-CLEAR-RECT";
        private bool _maxDebugStartupLogged;
        private static readonly Dictionary<int, string> MaxDebugLastState = new();
        private static readonly Dictionary<int, Vector3> MaxDebugExpectedLocal = new();
        private static readonly HashSet<int> MaxGroupsWithValidAnchor = new();

        public void Update()
        {
            if (!_maxDebugStartupLogged)
            {
                _maxDebugStartupLogged = true;
                LogMaxDebug(
                    $"BUILD {MaxDebugBuild} loaded. " +
                    $"2col=({WarehouseRefillPlugin.MaxLabelTwoColumnOffsetXValue:0.##}," +
                    $"{WarehouseRefillPlugin.MaxLabelTwoColumnOffsetYValue:0.##}) " +
                    $"scale={WarehouseRefillPlugin.MaxLabelTwoColumnTextScaleValue:0.##} " +
                    $"3col=({WarehouseRefillPlugin.MaxLabelThreeColumnOffsetXValue:0.##}," +
                    $"{WarehouseRefillPlugin.MaxLabelThreeColumnOffsetYValue:0.##}) " +
                    $"scale={WarehouseRefillPlugin.MaxLabelThreeColumnTextScaleValue:0.##}");
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                ClearCartNow();
            }

            // This component is now created only after Main Scene is loaded and
            // SalesItem.Start tells us that the Market product UI exists. Give the
            // native Market layout a short moment to finish its own layout pass,
            // then process queued cards directly. Do NOT wait for
            // _marketContentCache here: that cache is discovered by the slower
            // 0.5-second scan below and was blocking the whole MAX queue.
            _marketOpenReadyTimer += Time.deltaTime;

            if (_marketOpenReadyTimer >= 0.35f && UIQueue.Count > 0)
            {
                if (!_queueReadyLogged)
                {
                    _queueReadyLogged = true;
                    LogMaxDebug(
                        $"QUEUE READY delay={_marketOpenReadyTimer:0.00}s " +
                        $"queued={UIQueue.Count} marketCache={(_marketContentCache is not null)}");
                }

                int jobsProcessed = 0;
                int jobsToInspect = Mathf.Min(UIQueue.Count, 12);

                while (UIQueue.Count > 0 &&
                       jobsProcessed < 6 &&
                       jobsToInspect > 0)
                {
                    UIJob job = UIQueue[0];
                    UIQueue.RemoveAt(0);
                    jobsToInspect--;

                    if (job is null ||
                        job.Parent is null ||
                        job.Parent.gameObject is null)
                    {
                        jobsProcessed++;
                        continue;
                    }

                    // If Unity has created the SalesItem but its hierarchy is not
                    // active yet, keep the job for the next frame instead of losing it.
                    if (!job.Parent.gameObject.activeInHierarchy)
                    {
                        UIQueue.Add(job);
                        continue;
                    }

                    int parentId = job.Parent.GetInstanceID();

                    LogMaxDebug(
                        $"QUEUE PROCESS product={job.ProductId} " +
                        $"parent='{job.Parent.name}' id={parentId} " +
                        $"queuedBefore={UIQueue.Count + 1}");

                    BuildLightweightUI(job.Parent, job.ProductId, job.Font);
                    QueuedParents.Remove(parentId);
                    jobsProcessed++;
                }
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= 0.5f)
            {
                _checkTimer = 0f;
                if (_computer == null || _computer.gameObject == null)
                {
                    _computer = FindFirstObjectByType<Computer>(FindObjectsInactive.Include);
                    if (_computer == null)
                    {
                        return;
                    }

                    _marketContentCache = null;
                    _taskbarTransformCache = null;
                    _cartButtonTransformCache = null;
                    _buyingPanelCache = null;
                    _purchaseButtonCache = null;
                    _originalPositions.Clear();
                }

                FindMarketContent();
                CheckAndCreateRefillButton();
                CheckAndCreateResetLimitsButton();
                CheckAndCreateClearButton();
                ToggleRefillButtonVisibility();
                ApplyUIPositions();
                RefreshSmartLimitLayouts();
            }
        }

        private void CleanClonedButton(GameObject go)
        {
            Button button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
            }

            foreach (Component comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                string typeName = comp.GetIl2CppType().Name;
                if (typeName != "RectTransform" && typeName != "CanvasRenderer" && typeName != "Image" && typeName != "Button" &&
                    typeName != "LayoutElement" && typeName != "Transform" && typeName != "GameObject")
                {
                    try
                    {
                        Destroy(comp);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private Sprite LoadSpriteFromEmbedded(string fileName)
        {
            if (SpriteCache.ContainsKey(fileName) && SpriteCache[fileName] is not null)
            {
                return SpriteCache[fileName];
            }

            Sprite sprite = null;
            try
            {
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
                string resourcePath = null;
                foreach (string resourceName in manifestResourceNames)
                {
                    if (!resourceName.EndsWith(fileName))
                        continue;
                    resourcePath = resourceName;
                    break;
                }

                if (resourcePath is not null)
                {
                    using Stream resourceStream = executingAssembly.GetManifestResourceStream(resourcePath);
                    if (resourceStream is not null)
                    {
                        byte[] buffer = new byte[resourceStream.Length];
                        resourceStream.Read(buffer, 0, buffer.Length);
                        Il2CppStructArray<byte> il2CppBuffer = new Il2CppStructArray<byte>((long)buffer.Length);
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            il2CppBuffer[i] = buffer[i];
                        }

                        Texture2D texture = new Texture2D(2, 2);
                        bool isLoaded = false;
                        Type imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                        if (imageConversionType == null)
                        {
                            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                            foreach (Assembly assembly in assemblies)
                            {
                                try
                                {
                                    imageConversionType = assembly.GetType("UnityEngine.ImageConversion");
                                    if (imageConversionType is not null)
                                    {
                                        break;
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                        else
                        {
                            MethodInfo[] methods = imageConversionType.GetMethods();
                            foreach (MethodInfo method in methods)
                            {
                                if (method.Name != "LoadImage")
                                    continue;

                                ParameterInfo[] parameters = method.GetParameters();
                                if (parameters.Length < 2)
                                    continue;

                                try
                                {
                                    object dataToLoad = ((parameters[1].ParameterType == typeof(byte[])) ? buffer : il2CppBuffer);
                                    object loadResult;
                                    if (parameters.Length == 2)
                                    {
                                        loadResult = method.Invoke(null, new object[] { texture, dataToLoad });
                                    }
                                    else if (parameters.Length == 3)
                                    {
                                        loadResult = method.Invoke(null, new object[] { texture, dataToLoad, false });
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    isLoaded = !(loadResult is bool) || (bool)loadResult;
                                    if (isLoaded)
                                    {
                                        break;
                                    }
                                }
                                catch
                                {
                                }
                            }

                            if (isLoaded)
                            {
                                texture.filterMode = FilterMode.Bilinear;
                                Sprite loadedSprite = Sprite.Create(texture, new Rect(0f, 0f, (float)texture.width, (float)texture.height),
                                    new Vector2(0.5f, 0.5f));
                                SpriteCache[fileName] = loadedSprite;
                                sprite = loadedSprite;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return sprite;
        }

        private void ApplyUIPositions()
        {
            if (_taskbarTransformCache == null)
            {
                return;
            }

            float refillBtnOffsetX = -28f;
            float refillBtnOffsetY = 0f;
            float resetBtnOffsetX = -56f;
            float resetBtnOffsetY = 0f;
            float cartContainerOffsetX = 15f;
            float cartContainerOffsetY = 0f;
            float buttonsContainerOffsetX = -20f;
            float buttonsContainerOffsetY = 0f;

            if (_cartButtonTransformCache is not null)
            {
                Vector3 cartPos = (_originalPositions.ContainsKey("Cart Button")
                    ? _originalPositions["Cart Button"]
                    : _cartButtonTransformCache.localPosition);
                Transform refillBtn = _taskbarTransformCache.Find("WarehouseRefillButton");
                if (refillBtn is not null)
                {
                    refillBtn.localPosition = cartPos + new Vector3(refillBtnOffsetX, refillBtnOffsetY, 0f);
                }

                Transform resetBtn = _taskbarTransformCache.Find("ResetLimitsButton");
                if (resetBtn is not null)
                {
                    resetBtn.localPosition = cartPos + new Vector3(resetBtnOffsetX, resetBtnOffsetY, 0f);
                }
            }

            ApplyOffsetToContainer(_taskbarTransformCache, "Cart Button", cartContainerOffsetX, cartContainerOffsetY);
            ApplyOffsetToContainer(_taskbarTransformCache, "Buttons", buttonsContainerOffsetX, buttonsContainerOffsetY);
        }

        private void ApplyOffsetToContainer(Transform parent, string containerName, float offsetX, float offsetY)
        {
            Transform container = parent.Find(containerName);
            if (container is null)
            {
                return;
            }

            if (!_originalPositions.ContainsKey(containerName))
            {
                _originalPositions[containerName] = container.localPosition;
                LayoutElement layoutElement = container.GetComponent<LayoutElement>();
                if (layoutElement is null)
                {
                    layoutElement = container.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.ignoreLayout = true;
            }

            container.localPosition = _originalPositions[containerName] + new Vector3(offsetX, offsetY, 0f);
        }

        private void BuildLightweightUI(Transform parent, int productId, TMP_FontAsset font)
        {
            if (parent?.gameObject is null)
            {
                return;
            }

            if (parent.Find("SmartLimitButtonGroup") is not null)
            {
                return;
            }

            GameObject groupObj = new GameObject("SmartLimitButtonGroup");
            RectTransform groupRt = groupObj.AddComponent<RectTransform>();
            groupRt.SetParent(parent, false);
            groupRt.SetAsLastSibling();
            groupObj.layer = parent.gameObject.layer;
            LayoutElement layoutElement = groupObj.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            // Start with a safe fallback position. The final position is aligned
            // dynamically under the lowest stock-count number in the product card,
            // so it follows both the wide and compact market layouts.
            groupRt.anchorMin = new Vector2(1f, 0f);
            groupRt.anchorMax = new Vector2(1f, 0f);
            groupRt.pivot = new Vector2(1f, 0f);
            groupRt.anchoredPosition = new Vector2(-70f, 8f);
            groupRt.sizeDelta = new Vector2(58f, 16f);
            Button groupButton = groupObj.AddComponent<Button>();
            groupButton.targetGraphic = null;
            GameObject textObj = new GameObject("Text");
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.SetParent(groupObj.transform, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 10f;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.Right;
            tmpText.enableWordWrapping = false;
            tmpText.overflowMode = TextOverflowModes.Overflow;
            if (font is not null)
            {
                tmpText.font = font;
            }

            int currentLimit = WarehouseRefillPlugin.ProductLimits.TryGetValue(productId, out int userLimit)
                ? userLimit
                : GetMaxBoxCapacity(productId);
            tmpText.text = $"Max: {currentLimit}";

            LogMaxDebug(
                $"BUILD MAX product={productId} parent='{parent.name}' " +
                $"cardActive={parent.gameObject.activeInHierarchy}");

            RectTransform cardRt = parent.GetComponent<RectTransform>();
            if (cardRt is null)
            {
                LogMaxDebug(
                    $"CARD RECT MISSING product={productId} parent='{parent.name}' " +
                    $"transformType={parent.GetType().FullName}");
            }
            else
            {
                ApplySmartLimitLayout(cardRt, groupRt, tmpText);
            }
            _smartLimitGroups.Add(groupRt);
            groupButton.onClick.AddListener((Action)(() => { OpenGlobalEdit(productId, tmpText, groupRt, font); }));
        }

        private void RefreshSmartLimitLayouts()
        {
            // The UI manager is persistent, but MarketAppUIStateReset clears this
            // runtime list on a Single scene load. SalesItem cards can already have
            // their SmartLimitButtonGroup objects at that point, so the visible MAX
            // labels survive while the enhancer forgets their RectTransforms.
            // Re-adopt those existing groups before trying to refresh their layout.
            if (_smartLimitGroups.Count == 0)
            {
                AdoptExistingSmartLimitGroups();
            }

            for (int i = _smartLimitGroups.Count - 1; i >= 0; i--)
            {
                RectTransform groupRt = _smartLimitGroups[i];
                if (groupRt is null || groupRt.gameObject is null || groupRt.parent is null)
                {
                    _smartLimitGroups.RemoveAt(i);
                    continue;
                }

                RectTransform cardRt = groupRt.parent.GetComponent<RectTransform>();
                TextMeshProUGUI label = groupRt.GetComponentInChildren<TextMeshProUGUI>(true);
                if (cardRt is null || label is null)
                {
                    continue;
                }

                // Computer Fullscreen keeps both market-card layouts around and
                // toggles which hierarchy is active. Never reposition a hidden
                // layout: all of its native TMP counters are inactive at that
                // moment, which used to force our MAX label into FALLBACK.
                if (!cardRt.gameObject.activeInHierarchy ||
                    !groupRt.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ApplySmartLimitLayout(cardRt, groupRt, label);
            }
        }

        private void AdoptExistingSmartLimitGroups()
        {
            Transform root = _marketContentCache;
            if (root is null || root.gameObject is null)
            {
                return;
            }

            int adopted = 0;

            // FindMarketContent identifies Content by checking that its direct
            // children are SalesItem cards. Scanning those direct children is much
            // cheaper than walking every RectTransform below the market UI.
            for (int i = 0; i < root.childCount; i++)
            {
                Transform card = root.GetChild(i);
                if (card is null || card.gameObject is null)
                {
                    continue;
                }

                Transform existing = card.Find("SmartLimitButtonGroup");
                RectTransform groupRt = existing.GetComponent<RectTransform>();
                if (groupRt is null || groupRt.gameObject is null)
                {
                    continue;
                }

                _smartLimitGroups.Add(groupRt);
                adopted++;
            }

            if (adopted > 0)
            {
                LogMaxDebug(
                    $"ADOPT adopted={adopted} tracked={_smartLimitGroups.Count} " +
                    $"market={root.name} cards={root.childCount}");
            }
        }

        private static void ApplySmartLimitLayout(RectTransform cardRt, RectTransform groupRt, TextMeshProUGUI label)
        {
            if (cardRt is null || groupRt is null || label is null)
            {
                return;
            }

            int groupId = groupRt.GetInstanceID();

            if (MaxDebugExpectedLocal.TryGetValue(groupId, out Vector3 expectedBefore))
            {
                Vector3 actualBefore = groupRt.localPosition;
                if ((actualBefore - expectedBefore).sqrMagnitude > 0.25f)
                {
                    LogMaxDebug(
                        $"OVERWRITE group={groupRt.name} card={cardRt.name} " +
                        $"expectedLocal={FormatVector(expectedBefore)} actualBefore={FormatVector(actualBefore)}");
                }
            }

            if (TryPositionLimitUnderStockCount(cardRt, groupRt, label))
            {
                return;
            }

            // The game's market UI can briefly disable/clear Item Count Text while
            // it refreshes filters/layout. Once this MAX group has already been
            // positioned from a real native counter, keep that last good position
            // instead of jumping to FALLBACK. That jump was the visible flicker in
            // the 3-column view.
            if (MaxGroupsWithValidAnchor.Contains(groupId) &&
                MaxDebugExpectedLocal.TryGetValue(groupId, out Vector3 stableLocal))
            {
                groupRt.anchorMin = new Vector2(0.5f, 0.5f);
                groupRt.anchorMax = new Vector2(0.5f, 0.5f);
                groupRt.pivot = new Vector2(1f, 1f);
                groupRt.sizeDelta = new Vector2(58f, 16f);
                groupRt.localPosition = stableLocal;

                LogMaxState(
                    groupRt,
                    $"HOLD_LAST_VALID card={cardRt.name} " +
                    $"cardRect={FormatRect(cardRt.rect)} " +
                    $"local={FormatVector(stableLocal)}");
                return;
            }

            int columns = DetectMarketColumnCount(cardRt, out string reason);
            bool twoColumn = columns <= 2;
            float offsetX = twoColumn
                ? WarehouseRefillPlugin.MaxLabelTwoColumnOffsetXValue
                : WarehouseRefillPlugin.MaxLabelThreeColumnOffsetXValue;
            float offsetY = twoColumn
                ? WarehouseRefillPlugin.MaxLabelTwoColumnOffsetYValue
                : WarehouseRefillPlugin.MaxLabelThreeColumnOffsetYValue;

            groupRt.anchorMin = new Vector2(1f, 0f);
            groupRt.anchorMax = new Vector2(1f, 0f);
            groupRt.pivot = new Vector2(1f, 0f);
            groupRt.sizeDelta = new Vector2(58f, 16f);

            Vector2 beforeAnchored = groupRt.anchoredPosition;
            groupRt.anchoredPosition = new Vector2(
                -70f + offsetX,
                8f + offsetY);

            label.fontSize = 10f;
            MaxDebugExpectedLocal[groupId] = groupRt.localPosition;

            LogMaxState(
                groupRt,
                $"FALLBACK columns={columns} reason={reason} " +
                $"cardRect={FormatRect(cardRt.rect)} " +
                $"offset=({offsetX:0.##},{offsetY:0.##}) " +
                $"beforeAnchored={FormatVector(beforeAnchored)} " +
                $"afterAnchored={FormatVector(groupRt.anchoredPosition)} " +
                $"afterLocal={FormatVector(groupRt.localPosition)}");
        }

        private static bool TryPositionLimitUnderStockCount(RectTransform cardRt, RectTransform groupRt, TextMeshProUGUI label)
        {
            TextMeshProUGUI lowestIntegerText = null;
            float lowestY = float.PositiveInfinity;
            float rightmostX = float.NegativeInfinity;
            int integerCandidates = 0;

            foreach (TextMeshProUGUI candidate in cardRt.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (candidate is null || candidate == label || !candidate.gameObject.activeInHierarchy ||
                    candidate.transform.IsChildOf(groupRt))
                {
                    continue;
                }

                if (candidate.GetComponentInParent<TMP_InputField>() is not null)
                {
                    continue;
                }

                string value = candidate.text?.Trim();
                if (string.IsNullOrEmpty(value) || !int.TryParse(value, out _))
                {
                    continue;
                }

                integerCandidates++;

                RectTransform candidateRt = candidate.rectTransform;
                if (candidateRt is null)
                {
                    continue;
                }

                Vector3 centerWorld = candidateRt.TransformPoint(
                    new Vector3(candidateRt.rect.center.x, candidateRt.rect.center.y, 0f));
                Vector3 centerLocal = cardRt.InverseTransformPoint(centerWorld);
                if (!cardRt.rect.Contains(new Vector2(centerLocal.x, centerLocal.y)))
                {
                    continue;
                }

                if (centerLocal.y < lowestY - 0.5f ||
                    (Mathf.Abs(centerLocal.y - lowestY) <= 0.5f && centerLocal.x > rightmostX))
                {
                    lowestIntegerText = candidate;
                    lowestY = centerLocal.y;
                    rightmostX = centerLocal.x;
                }
            }

            if (lowestIntegerText is null)
            {
                LogMaxState(
                    groupRt,
                    $"NO_ANCHOR card={cardRt.name} cardRect={FormatRect(cardRt.rect)} " +
                    $"integerCandidates={integerCandidates}");
                return false;
            }

            RectTransform stockCountRt = lowestIntegerText.rectTransform;
            Vector3 bottomRightWorld = stockCountRt.TransformPoint(
                new Vector3(stockCountRt.rect.xMax, stockCountRt.rect.yMin, 0f));
            Vector3 bottomRightLocal = cardRt.InverseTransformPoint(bottomRightWorld);

            int columns = DetectMarketColumnCount(cardRt, out string reason);
            bool twoColumn = columns <= 2;
            float offsetX = twoColumn
                ? WarehouseRefillPlugin.MaxLabelTwoColumnOffsetXValue
                : WarehouseRefillPlugin.MaxLabelThreeColumnOffsetXValue;
            float offsetY = twoColumn
                ? WarehouseRefillPlugin.MaxLabelTwoColumnOffsetYValue
                : WarehouseRefillPlugin.MaxLabelThreeColumnOffsetYValue;

            Vector3 beforeLocal = groupRt.localPosition;
            Vector2 beforeAnchored = groupRt.anchoredPosition;

            groupRt.anchorMin = new Vector2(0.5f, 0.5f);
            groupRt.anchorMax = new Vector2(0.5f, 0.5f);
            groupRt.pivot = new Vector2(1f, 1f);
            groupRt.sizeDelta = new Vector2(58f, 16f);
            groupRt.localPosition = new Vector3(
                bottomRightLocal.x + offsetX,
                bottomRightLocal.y + offsetY,
                0f);

            float textScale = twoColumn
                ? WarehouseRefillPlugin.MaxLabelTwoColumnTextScaleValue
                : WarehouseRefillPlugin.MaxLabelThreeColumnTextScaleValue;

            label.fontSize = Mathf.Clamp(
                lowestIntegerText.fontSize * textScale,
                MaxFontAbsoluteMin,
                MaxFontAbsoluteMax);

            int groupId = groupRt.GetInstanceID();
            MaxDebugExpectedLocal[groupId] = groupRt.localPosition;
            MaxGroupsWithValidAnchor.Add(groupId);

            LogMaxState(
                groupRt,
                $"NORMAL columns={columns} reason={reason} " +
                $"card={cardRt.name} cardRect={FormatRect(cardRt.rect)} " +
                $"anchorText='{lowestIntegerText.text}' anchorName={lowestIntegerText.gameObject.name} " +
                $"anchorLocalBR={FormatVector(bottomRightLocal)} " +
                $"offset=({offsetX:0.##},{offsetY:0.##}) " +
                $"beforeLocal={FormatVector(beforeLocal)} afterLocal={FormatVector(groupRt.localPosition)} " +
                $"beforeAnchored={FormatVector(beforeAnchored)} afterAnchored={FormatVector(groupRt.anchoredPosition)}");
            return true;
        }

        private static int DetectMarketColumnCount(RectTransform cardRt, out string reason)
        {
            Transform current = cardRt;
            for (int depth = 0; depth < 8 && current is not null; depth++, current = current.parent)
            {
                GridLayoutGroup grid = current.GetComponent<GridLayoutGroup>();
                if (grid is null)
                {
                    continue;
                }

                if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount &&
                    grid.constraintCount > 0)
                {
                    reason = $"grid-fixed:{grid.constraintCount}@{current.name}";
                    return Mathf.Clamp(grid.constraintCount, 1, 6);
                }

                RectTransform gridRt = current.GetComponent<RectTransform>();
                if (gridRt is not null && grid.cellSize.x > 1f)
                {
                    float usableWidth =
                        gridRt.rect.width -
                        grid.padding.left -
                        grid.padding.right;

                    float stride = grid.cellSize.x + grid.spacing.x;
                    if (usableWidth > 1f && stride > 1f)
                    {
                        int calculated = Mathf.Max(
                            1,
                            Mathf.FloorToInt((usableWidth + grid.spacing.x + 0.5f) / stride));

                        reason =
                            $"grid-calc:{calculated}@{current.name}" +
                            $"[w={usableWidth:0.##},cell={grid.cellSize.x:0.##},space={grid.spacing.x:0.##}]";
                        return Mathf.Clamp(calculated, 1, 6);
                    }
                }
            }

            float width = cardRt.rect.width;
            int fallbackColumns = width >= 400f ? 2 : 3;
            reason = $"card-width:{width:0.##}";
            return fallbackColumns;
        }

        private static void LogMaxState(RectTransform groupRt, string state)
        {
            if (groupRt is null)
            {
                return;
            }

            int id = groupRt.GetInstanceID();
            if (MaxDebugLastState.TryGetValue(id, out string previous) &&
                previous == state)
            {
                return;
            }

            MaxDebugLastState[id] = state;
            LogMaxDebug(state);
        }

        private static void LogMaxDebug(string message)
        {
            try
            {
                WarehouseRefillPlugin plugin = WarehouseRefillPlugin.Instance;
                if (plugin is not null)
                {
                    plugin.Log.LogInfo($"[MAXDBG] {message}");
                }
            }
            catch
            {
            }
        }

        private static string FormatVector(Vector2 value)
        {
            return $"({value.x:0.##},{value.y:0.##})";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.##},{value.y:0.##},{value.z:0.##})";
        }

        private static string FormatRect(Rect value)
        {
            return $"({value.x:0.##},{value.y:0.##},{value.width:0.##},{value.height:0.##})";
        }

        private static void OpenGlobalEdit(int productId, TextMeshProUGUI textComp, RectTransform targetRt, TMP_FontAsset font)
        {
            if (_globalInputObj is null)
            {
                _globalInputObj = new GameObject("SmartLimit_GlobalInput");
                RectTransform globalInputRt = _globalInputObj.AddComponent<RectTransform>();
                globalInputRt.sizeDelta = new Vector2(40f, 22f);
                globalInputRt.anchorMin = new Vector2(1f, 0.5f);
                globalInputRt.anchorMax = new Vector2(1f, 0.5f);
                globalInputRt.pivot = new Vector2(1f, 0.5f);
                Image bgImage = _globalInputObj.AddComponent<Image>();
                bgImage.color = new Color(0f, 0f, 0f, 0f);
                GameObject textObj = new GameObject("Text");
                RectTransform textRt = textObj.AddComponent<RectTransform>();
                textRt.SetParent(_globalInputObj.transform, false);
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
                tmpText.color = Color.clear;
                if (font is not null)
                {
                    tmpText.font = font;
                }

                _globalInput = _globalInputObj.AddComponent<TMP_InputField>();
                _globalInput.textComponent = tmpText;
                _globalInput.textViewport = textRt;
                _globalInput.characterLimit = 5;
                _globalInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                _globalInput.customCaretColor = true;
                _globalInput.caretColor = Color.clear;
                _globalInput.onValueChanged.AddListener((Action<string>)OnGlobalInputValueChanged);
                _globalInput.onEndEdit.AddListener((Action<string>)OnGlobalInputEndEdit);
            }

            _editingProductId = productId;
            _editingText = textComp;
            _globalInputObj.SetActive(true);
            _globalInputObj.transform.SetParent(targetRt, false);
            _globalInputObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            _globalInput.text = "";
            _editingText.text = "Max: _";
            _globalInput.Select();
            _globalInput.ActivateInputField();
        }

        private static void OnGlobalInputValueChanged(string val)
        {
            if (_editingText is not null)
            {
                _editingText.text = string.IsNullOrEmpty(val) ? "Max: _" : $"Max: {val}";
            }
        }

        private static void OnGlobalInputEndEdit(string val)
        {
            if (_editingProductId == -1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(val) || val == "0")
            {
                if (WarehouseRefillPlugin.ProductLimits.Remove(_editingProductId))
                {
                    List<string> limitLines = new List<string>();
                    foreach (var kvp in WarehouseRefillPlugin.ProductLimits)
                    {
                        limitLines.Add($"{kvp.Key}:{kvp.Value}");
                    }

                    try
                    {
                        File.WriteAllLines(WarehouseRefillPlugin.LimitFilePath, limitLines);
                    }
                    catch
                    {
                    }
                }

                int defaultCapacity = GetMaxBoxCapacity(_editingProductId);
                if (_editingText is not null)
                {
                    _editingText.text = $"Max: {defaultCapacity}";
                }
            }
            else if (int.TryParse(val, out int inputLimit))
            {
                int maxCap = GetMaxBoxCapacity(_editingProductId);
                int finalLimit = Mathf.Clamp(inputLimit, 1, maxCap);
                WarehouseRefillPlugin.SaveLimit(_editingProductId, finalLimit);
                if (_editingText is not null)
                {
                    _editingText.text = $"Max: {finalLimit}";
                }
            }

            _globalInputObj.SetActive(false);
            _editingProductId = -1;
            _editingText = null;
        }

        private static int GetMaxBoxCapacity(int productId)
        {
            IDManager idManager = FindFirstObjectByType<IDManager>();
            RackManager rackManager = FindFirstObjectByType<RackManager>();
            if (idManager is null || rackManager is null)
                return 99;

            ProductSO productSO = idManager.ProductSO(productId);
            if (productSO is null)
                return 99;

            int totalCapacity = 0;
            bool foundDesignatedRack = false;

            foreach (Rack rack in rackManager.m_Racks)
            {
                if (rack is null)
                    continue;
                foreach (RackSlot rackSlot in rack.RackSlots)
                {
                    if (rackSlot is null || rackSlot.Data is null)
                        continue;
                    if (rackSlot.Data.ProductID == productId)
                    {
                        foundDesignatedRack = true;
                        int slotMax = BoxCapacityHelper.GetSlotCapacity(productSO);
                        if (rackSlot.Data.BoxCount > slotMax)
                            slotMax = rackSlot.Data.BoxCount;
                        totalCapacity += slotMax;
                    }
                }
            }

            if (!foundDesignatedRack)
            {
                return BoxCapacityHelper.GetSlotCapacity(productSO);
            }

            return totalCapacity;
        }

        private void ToggleRefillButtonVisibility()
        {
            if (_taskbarTransformCache?.gameObject is not null)
            {
                Transform refillBtn = _taskbarTransformCache.Find("WarehouseRefillButton");
                Transform resetBtn = _taskbarTransformCache.Find("ResetLimitsButton");
                bool isMarketActive = _marketContentCache?.gameObject is not null && _marketContentCache.gameObject.activeInHierarchy;
                if (refillBtn is not null && refillBtn.gameObject.activeSelf != isMarketActive)
                {
                    refillBtn.gameObject.SetActive(isMarketActive);
                }

                if (resetBtn is not null && resetBtn.gameObject.activeSelf != isMarketActive)
                {
                    resetBtn.gameObject.SetActive(isMarketActive);
                }
            }
        }

        private void FindMarketContent()
        {
            if (_computer is null)
                return;

            bool isMarketActive = _marketContentCache?.gameObject is not null && _marketContentCache.gameObject.activeInHierarchy;
            if (!isMarketActive)
            {
                foreach (LayoutGroup group in _computer.GetComponentsInChildren<LayoutGroup>(false))
                {
                    if (group.name == "Content" && group.transform.childCount > 0)
                    {
                        for (int i = 0; i < Mathf.Min(group.transform.childCount, 3); i++)
                        {
                            foreach (Component comp in group.transform.GetChild(i).GetComponents<Component>())
                            {
                                if (comp is not null && comp.GetIl2CppType().Name == "SalesItem")
                                {
                                    _marketContentCache = group.transform;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CheckAndCreateRefillButton()
        {
            if (_taskbarTransformCache?.gameObject is null)
            {
                foreach (Transform trans in _computer.GetComponentsInChildren<Transform>(true))
                {
                    if (trans.name == "Taskbar")
                    {
                        _taskbarTransformCache = trans;
                        break;
                    }
                }
            }

            if (_taskbarTransformCache is not null)
            {
                if (_taskbarTransformCache.Find("WarehouseRefillButton") is null)
                {
                    if (_cartButtonTransformCache?.gameObject is null)
                    {
                        foreach (Button btn in _taskbarTransformCache.GetComponentsInChildren<Button>(true))
                        {
                            if (btn.name.Contains("Cart"))
                            {
                                _cartButtonTransformCache = btn.transform;
                                break;
                            }
                        }
                    }

                    if (_cartButtonTransformCache != null)
                    {
                        GameObject refillBtnObj = Instantiate(_cartButtonTransformCache.gameObject, _taskbarTransformCache);
                        refillBtnObj.name = "WarehouseRefillButton";
                        CleanClonedButton(refillBtnObj);
                        LayoutElement layout = refillBtnObj.GetComponent<LayoutElement>() ?? refillBtnObj.AddComponent<LayoutElement>();
                        layout.ignoreLayout = true;
                        float offsetX = -28f;
                        float offsetY = 0f;
                        refillBtnObj.transform.localPosition = _cartButtonTransformCache.localPosition + new Vector3(offsetX, offsetY, 0f);
                        Button refillButton = refillBtnObj.GetComponent<Button>();
                        Image refillImage = refillBtnObj.GetComponent<Image>();
                        if (refillImage is not null)
                        {
                            refillImage.color = new Color(0.9607843f, 0.6509804f, 0.13725491f, 1f);
                        }

                        Transform iconTrans = refillBtnObj.transform.Find("Icon");
                        if (iconTrans is not null)
                        {
                            Image iconImage = iconTrans.GetComponent<Image>();
                            Sprite refillSprite = LoadSpriteFromEmbedded("refill_icon.png");
                            if (iconImage is not null && refillSprite is not null)
                            {
                                iconImage.sprite = refillSprite;
                            }
                        }

                        refillButton.onClick.AddListener(new Action(OnRefillClick));
                    }
                }
            }
        }

        private void CheckAndCreateResetLimitsButton()
        {
            if (_taskbarTransformCache is not null)
            {
                if (_taskbarTransformCache.Find("ResetLimitsButton") is null)
                {
                    if (_cartButtonTransformCache is not null)
                    {
                        GameObject resetBtnObj = Instantiate(_cartButtonTransformCache.gameObject, _taskbarTransformCache);
                        resetBtnObj.name = "ResetLimitsButton";
                        CleanClonedButton(resetBtnObj);
                        LayoutElement layout = resetBtnObj.GetComponent<LayoutElement>() ?? resetBtnObj.AddComponent<LayoutElement>();
                        layout.ignoreLayout = true;
                        float offsetX = -56f;
                        float offsetY = 0f;
                        resetBtnObj.transform.localPosition = _cartButtonTransformCache.localPosition + new Vector3(offsetX, offsetY, 0f);
                        Button resetButton = resetBtnObj.GetComponent<Button>();
                        Image resetImage = resetBtnObj.GetComponent<Image>();
                        if (resetImage is not null)
                        {
                            resetImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
                        }

                        Transform iconTrans = resetBtnObj.transform.Find("Icon");
                        if (iconTrans is not null)
                        {
                            Image iconImage = iconTrans.GetComponent<Image>();
                            Sprite resetSprite = LoadSpriteFromEmbedded("reset_icon.png");
                            if (iconImage is not null && resetSprite is not null)
                            {
                                iconImage.sprite = resetSprite;
                            }
                        }

                        resetButton.onClick.AddListener(new Action(ClearAllLimits));
                        TextMeshProUGUI btnText = resetBtnObj.GetComponentInChildren<TextMeshProUGUI>();
                        if (btnText is not null)
                        {
                            btnText.text = "RESET LIMITS";
                        }
                    }
                }
            }
        }

        private void ClearAllLimits()
        {
            WarehouseRefillPlugin.ProductLimits.Clear();
            try
            {
                File.WriteAllText(WarehouseRefillPlugin.LimitFilePath, "");
            }
            catch
            {
            }

            if (_marketContentCache != null)
            {
                foreach (TextMeshProUGUI tmpText in _marketContentCache.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmpText.text.StartsWith("Max: ") && tmpText.transform.parent.name == "SmartLimitButtonGroup")
                    {
                        tmpText.text = "Max: R";
                    }
                }
            }
        }

        private void CheckAndCreateClearButton()
        {
            if (_cart?.gameObject is null)
            {
                _cart = FindFirstObjectByType<MarketShoppingCart>(FindObjectsInactive.Include);
            }

            if (_cart is null ||
                _cart.gameObject is null ||
                !_cart.gameObject.activeInHierarchy)
            {
                return;
            }

            // The old button was created next to the Purchase button and could
            // overlap the totals/order panel. The cart popup already has a stable
            // close button in its top-right header, so use that as the anchor.
            Transform existingClear = FindCartDescendantByName("ClearCartButton");
            if (existingClear is not null)
            {
                return;
            }

            Button closeButton = FindCartCloseButton();
            if (closeButton is null ||
                closeButton.gameObject is null ||
                !closeButton.gameObject.activeInHierarchy)
            {
                return;
            }

            InjectClearButtonNextToClose(closeButton);
        }

        private Transform FindCartDescendantByName(string objectName)
        {
            if (_cart is null || _cart.gameObject is null)
            {
                return null;
            }

            foreach (Transform trans in _cart.GetComponentsInChildren<Transform>(true))
            {
                if (trans is not null && trans.name == objectName)
                {
                    return trans;
                }
            }

            return null;
        }

        private Button FindCartCloseButton()
        {
            if (_cart is null || _cart.gameObject is null)
            {
                return null;
            }

            Button fallback = null;
            float fallbackY = float.MinValue;
            float fallbackX = float.MinValue;

            foreach (Button btn in _cart.GetComponentsInChildren<Button>(true))
            {
                if (btn is null ||
                    btn.gameObject is null ||
                    !btn.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string lowerName = (btn.name ?? string.Empty).ToLowerInvariant();

                if (lowerName.Contains("clearcart") ||
                    lowerName == "remove button" ||
                    lowerName == "purchase button")
                {
                    continue;
                }

                // Prefer semantic names used by the native popup.
                if (lowerName.Contains("close") ||
                    lowerName.Contains("exit"))
                {
                    return btn;
                }

                // Some game prefabs use a generic button name and put only an X
                // glyph in the child TMP object.
                foreach (TextMeshProUGUI tmp in btn.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp is null)
                    {
                        continue;
                    }

                    string glyph = (tmp.text ?? string.Empty).Trim();
                    if (glyph == "X" ||
                        glyph == "x" ||
                        glyph == "×" ||
                        glyph == "✕" ||
                        glyph == "✖")
                    {
                        return btn;
                    }
                }

                // Final fallback: the native close button is the highest/rightmost
                // active button in the cart popup.
                Vector3 worldPos = btn.transform.position;
                if (worldPos.y > fallbackY + 0.001f ||
                    (Mathf.Abs(worldPos.y - fallbackY) <= 0.001f &&
                     worldPos.x > fallbackX))
                {
                    fallback = btn;
                    fallbackY = worldPos.y;
                    fallbackX = worldPos.x;
                }
            }

            return fallback;
        }

        private void InjectClearButtonNextToClose(Button closeButton)
        {
            if (closeButton is null || closeButton.transform.parent is null)
            {
                return;
            }

            Transform parent = closeButton.transform.parent;
            if (parent.Find("ClearCartButton") is not null)
            {
                return;
            }

            RectTransform closeRt = closeButton.GetComponent<RectTransform>();
            if (closeRt is null)
            {
                return;
            }

            const float clearWidth = 150f;
            const float gap = 12f;

            float closeWidth = Mathf.Max(40f, closeRt.rect.width);
            float closeHeight = Mathf.Max(40f, closeRt.rect.height);

            GameObject clearBtnObj = new GameObject("ClearCartButton");
            clearBtnObj.transform.SetParent(parent, false);

            RectTransform clearRt = clearBtnObj.AddComponent<RectTransform>();
            clearRt.anchorMin = closeRt.anchorMin;
            clearRt.anchorMax = closeRt.anchorMax;
            clearRt.pivot = closeRt.pivot;
            clearRt.localScale = Vector3.one;
            clearRt.localRotation = Quaternion.identity;
            clearRt.sizeDelta = new Vector2(clearWidth, closeHeight);

            // Place it immediately to the LEFT of the native X button.
            clearRt.anchoredPosition = closeRt.anchoredPosition +
                                       new Vector2(
                                           -((closeWidth * 0.5f) + gap + (clearWidth * 0.5f)),
                                           0f);

            LayoutElement layout =
                clearBtnObj.GetComponent<LayoutElement>() ??
                clearBtnObj.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            Image closeImg = closeButton.GetComponent<Image>();
            Image clearImg = clearBtnObj.AddComponent<Image>();

            // Deliberately DO NOT copy the native X-button sprite. That sprite
            // has rounded corners. With no sprite the Unity Image is rendered as
            // a plain solid rectangle.
            clearImg.sprite = null;
            clearImg.type = Image.Type.Simple;
            clearImg.preserveAspect = false;

            // Keep the same red family as the native close button.
            clearImg.color = closeImg is not null
                ? closeImg.color
                : new Color(0.9f, 0.15f, 0.12f, 1f);

            Button clearBtn = clearBtnObj.AddComponent<Button>();
            clearBtn.targetGraphic = clearImg;
            clearBtn.onClick.AddListener(new Action(ClearCartNow));

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(clearBtnObj.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 2f);
            textRt.offsetMax = new Vector2(-8f, -2f);

            TextMeshProUGUI clearText = textObj.AddComponent<TextMeshProUGUI>();
            clearText.text = "Clear all";
            clearText.color = Color.white;
            clearText.alignment = TextAlignmentOptions.Center;
            clearText.enableWordWrapping = false;
            clearText.overflowMode = TextOverflowModes.Ellipsis;

            // Reuse a native font from the cart header/button when possible.
            TextMeshProUGUI sourceText =
                closeButton.GetComponentInChildren<TextMeshProUGUI>(true);

            if (sourceText is null && parent is not null)
            {
                foreach (TextMeshProUGUI tmp in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp is not null && tmp.font is not null)
                    {
                        sourceText = tmp;
                        break;
                    }
                }
            }

            if (sourceText is not null)
            {
                clearText.font = sourceText.font;
            }

            clearText.fontSize = Mathf.Clamp(closeHeight * 0.36f, 16f, 24f);

            // Keep both custom and native close buttons above other header content.
            clearBtnObj.transform.SetAsLastSibling();
            closeButton.transform.SetAsLastSibling();
        }

        private void ClearCartNow()
        {
            if (_computer is null)
                _computer = FindFirstObjectByType<Computer>();

            if (_computer is not null)
                foreach (Button btn in _computer.GetComponentsInChildren<Button>(true))
                    if (btn.name == "Remove Button" && btn.gameObject.activeInHierarchy)
                        btn.onClick.Invoke();
        }

        private void OnRefillClick()
        {
            if (_cart?.gameObject is null)
                _cart = FindFirstObjectByType<MarketShoppingCart>(FindObjectsInactive.Include);

            if (_cart is not null)
                WarehouseRefillService.RefillWarehouse(_cart);
        }
    }
}