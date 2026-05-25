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

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                ClearCartNow();
            }

            bool hasQueue = UIQueue.Count > 0 && _marketContentCache != null && _marketContentCache.gameObject != null &&
                            _marketContentCache.gameObject.activeInHierarchy;
            if (hasQueue)
            {
                int jobsProcessed = 0;
                while (UIQueue.Count > 0 && jobsProcessed < 6)
                {
                    UIJob job = UIQueue[0];
                    UIQueue.RemoveAt(0);
                    if (job != null && job.Parent != null && job.Parent.gameObject != null)
                    {
                        BuildLightweightUI(job.Parent, job.ProductId, job.Font);
                        QueuedParents.Remove(job.Parent.GetInstanceID());
                    }

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
            groupRt.anchorMin = new Vector2(1f, 0.5f);
            groupRt.anchorMax = new Vector2(1f, 0.5f);
            groupRt.pivot = new Vector2(1f, 0.5f);
            groupRt.anchoredPosition = new Vector2(-65f, -37f);
            groupRt.sizeDelta = new Vector2(48f, 22f);
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
            if (font is not null)
            {
                tmpText.font = font;
            }

            int currentLimit = WarehouseRefillPlugin.ProductLimits.TryGetValue(productId, out int userLimit)
                ? userLimit
                : GetMaxBoxCapacity(productId);
            tmpText.text = $"Max: {currentLimit}";
            groupButton.onClick.AddListener((Action)(() => { OpenGlobalEdit(productId, tmpText, groupRt, font); }));
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

            if (_cart is not null)
            {
                if (_buyingPanelCache?.gameObject is null || _purchaseButtonCache?.gameObject is null)
                {
                    foreach (Transform trans in _computer.transform.GetComponentsInChildren<Transform>(true))
                    {
                        if (trans.name == "Purchase Button" && trans.parent is not null && trans.parent.name == "Buying Panel")
                        {
                            _purchaseButtonCache = trans;
                            _buyingPanelCache = trans.parent;
                            break;
                        }
                    }
                }

                if (_buyingPanelCache is not null && _buyingPanelCache.gameObject.activeInHierarchy)
                {
                    if (_buyingPanelCache.parent.Find("ClearCartButton") == null)
                    {
                        InjectClearButton(_buyingPanelCache, _purchaseButtonCache);
                    }
                }
            }
        }

        private void InjectClearButton(Transform buyingPanel, Transform originalPurchaseButton)
        {
            Transform parent = buyingPanel.parent;
            if (parent.Find("ClearCartButton") is not null)
                return;

            GameObject clearBtnObj = new GameObject("ClearCartButton");
            clearBtnObj.transform.SetParent(parent, false);
            RectTransform originalRt = originalPurchaseButton.GetComponent<RectTransform>();
            RectTransform clearRt = clearBtnObj.AddComponent<RectTransform>();
            clearRt.sizeDelta = originalRt.sizeDelta;
            clearBtnObj.transform.position = originalPurchaseButton.position;
            clearBtnObj.transform.localPosition += new Vector3(0f, 60f, 0f);
            Image originalImg = originalPurchaseButton.GetComponent<Image>();
            Image clearImg = clearBtnObj.AddComponent<Image>();
            if (originalImg is not null)
            {
                clearImg.sprite = originalImg.sprite;
                clearImg.type = originalImg.type;
            }

            clearImg.color = new Color(0.8f, 0.2f, 0.2f, 0.95f);
            Button clearBtn = clearBtnObj.AddComponent<Button>();
            clearBtn.targetGraphic = clearImg;
            clearBtn.onClick.AddListener(new Action(ClearCartNow));
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(clearBtnObj.transform, false);
            TextMeshProUGUI originalText = originalPurchaseButton.GetComponentInChildren<TextMeshProUGUI>();
            TextMeshProUGUI clearText = textObj.AddComponent<TextMeshProUGUI>();
            if (originalText is not null)
            {
                clearText.font = originalText.font;
                clearText.fontSize = originalText.fontSize;
                clearText.alignment = originalText.alignment;
            }

            clearText.text = "CLEAR CART";
            clearText.color = Color.white;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
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