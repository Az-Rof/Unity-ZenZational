using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Buff selection menu — CANVAS version (the UI is built in the Unity editor, not via code).
///
/// ⚠️ IMPORTANT: Because this menu object is INACTIVE at start (to keep it hidden),
/// Unity never calls its Start(). Therefore the connection to BuffSystem is NOT via
/// event subscription — but via DIRECT CALLS:
/// BuffSystem calls this script's ShowChoices() when the menu opens.
///
///   Unity menu: Tools → ZenZational → Build Buff Menu
///   (creates all objects + automatic wiring; freely edit the visuals afterwards)
///
/// MANUAL SETUP in the scene:
/// 1. Canvas → child "BuffMenu" (semi-transparent black Panel, full stretch, INACTIVE)
/// 2. Inside it: Title Text + 3 cards (Button + 3 Texts: Title/Description/StatPreview)
/// 3. Add the BuffSelectionUI component to "BuffMenu" and assign:
///    Menu Root, Title Text, Cards (Root/Button/Title/Description/StatPreview of each card)
/// 4. Make sure a BuffSystem component exists in the scene (e.g. object "BuffManager")
///
/// RUNTIME FLOW:
/// 1. BuffSystem.TriggerBuffSelection() → calls BuffSelectionUI.ShowChoices(3 random buffs)
/// 2. This script: pause game → populate cards → activate the menu
/// 3. Click a card / press 1-2-3 → BuffSystem.SelectBuff(...) → stats change
/// 4. BuffSystem calls HideMenu() → unpause → game continues
/// </summary>
public class BuffSelectionUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticStateForNewPlaySession()
    {
        // Enter Play Mode can be configured with domain reload disabled. Reset
        // static values explicitly so a previous session cannot lock gun input.
        Instance = null;
        isOpen = false;
    }

    /// <summary> One buff card slot — assign its references in the Inspector. </summary>
    [System.Serializable]
    public class BuffCardSlot
    {
        [Tooltip("Card root object (automatically hidden if there are fewer buffs than slots)")]
        public GameObject root;
        [Tooltip("Card button — used for click selection")]
        public Button button;
        [Tooltip("Category label text above the card, e.g. 'CHARACTER' / 'GUN'")]
        public TMP_Text categoryLabel;
        [Tooltip("Buff name text, e.g. 'Hollow Points'")]
        public TMP_Text titleText;
        [Tooltip("Buff description text, e.g. '+25% Bullet Damage'")]
        public TMP_Text descriptionText;
        [Tooltip("Stat preview text, e.g. 'DMG 15.0 -> 18.8'")]
        public TMP_Text statPreviewText;

        [System.NonSerialized] public GameObject configuredRoot;
        [System.NonSerialized] public GameObject runtimeRoot;
        [System.NonSerialized] public GameObject boundPrefab;
        public bool IsAssigned => root != null || button != null || titleText != null || descriptionText != null;
    }

    public static BuffSelectionUI Instance { get; private set; }

    /// <summary> True while the menu is open. Gameplay scripts (gun, etc.) check this. </summary>
    public static bool IsOpen => isOpen
        && Instance != null
        && Instance.isActiveAndEnabled
        && Instance.IsMenuVisible;

    [Header("Canvas References (required)")]
    [Tooltip("Menu root — the object that gets shown/hidden. Empty = use this script's own GameObject")]
    [SerializeField] private GameObject menuRoot;
    [Tooltip("Menu title text (optional)")]
    [SerializeField] private Text titleText;
    [Tooltip("Card slots — the count matches the cards in the scene (usually 3)")]
    [SerializeField] private BuffCardSlot[] cards;
    [Tooltip("Automatically map existing card prefabs using their child names (BuffName, Description, Choose, etc.)")]
    [SerializeField] private bool autoDiscoverCardReferences = true;
    [Tooltip("Use the matching prebuilt card prefab for each selected buff")]
    [SerializeField] private bool usePrebuiltCardPrefabs = true;


    [Header("Pause While Menu Is Open")]
    [Tooltip("Freeze gameplay (Time.timeScale = 0) while choosing a buff")]
    [SerializeField] private bool pauseGameWhileOpen = true;
    [Tooltip("Force the cursor to be visible while the menu is open")]
    [SerializeField] private bool showCursorWhileOpen = true;

    [Header("Card Category Colors")]
    [Tooltip("When enabled, the script replaces each card's designed color with the category color below")]
    [SerializeField] private bool tintCardsByCategory = false;
    [Tooltip("Card color for player stat buffs")]
    [SerializeField] private Color characterCardColor = new Color(0.14f, 0.20f, 0.14f, 1f);
    [Tooltip("Card color for weapon buffs")]
    [SerializeField] private Color gunCardColor = new Color(0.14f, 0.15f, 0.24f, 1f);
    [Tooltip("Card color for weapon swaps")]
    private static readonly Key[] SelectKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

    private readonly List<BuffOption> currentChoices = new List<BuffOption>();
    private static bool isOpen;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;

    private bool IsMenuVisible => menuRoot != null && menuRoot.activeInHierarchy;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BuffSelectionUI] Another instance already exists — this object is destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Static state is reset by SubsystemRegistration before a new Play Mode
        // session. Do not reset isOpen here: ShowChoices() sets it before calling
        // SetActive(true), and activating an initially inactive menu invokes Awake.
        previousTimeScale = 1f;
        previousCursorVisible = false;

        if (menuRoot == null) menuRoot = gameObject;
        DiscoverCardReferences();

        if (cards == null || cards.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Card slots 'Cards' are not assigned in the Inspector! " +
                             "The menu cannot display buff choices. Use Tools → ZenZational → Build Buff Menu " +
                             "for automatic setup.", this);
        }

        // Make sure the menu is hidden when the game starts.
        // IMPORTANT: do not hide the menu while it is being OPENED — SetActive(true)
        // from ShowChoices() triggers Awake() on first activation, and without
        // this guard the menu would immediately close again (bug: the menu flashes
        // for a moment then disappears, the game freezes, buttons cannot be clicked).
        if (!IsOpen) SetVisible(false);
    }

    void OnEnable()
    {
        // Because this object is usually inactive, OnEnable only runs when the menu opens.
        // Do not subscribe to any events here — the connection to BuffSystem happens
        // via direct calls (ShowChoices/HideMenu) from BuffSystem.

        // If the panel was manually re-enabled in the Inspector, do not display
        // stale card content from the previous session. ShowChoices() sets isOpen
        // before enabling the panel, so a legitimate menu opening is unaffected.
        if (!isOpen && menuRoot != null && menuRoot == gameObject)
        {
            menuRoot.SetActive(false);
        }
    }

    void OnDisable()
    {
        // If the panel is disabled manually while the menu is open, Unity does
        // not call HideMenu(). Restore the global state here so the gun is not
        // permanently blocked and the game does not remain paused.
        if (isOpen)
        {
            CloseAndRestorePreviousState();
        }
    }

    void OnDestroy()
    {
        // Unity may destroy the object while the menu is open when Play Mode stops
        // or the scene reloads. Always restore global gameplay state first.
        CloseAndRestorePreviousState();

        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit()
    {
        CloseAndRestorePreviousState();
    }

    void Update()
    {
        // Recover if an external script or the Inspector disabled the panel
        // without going through HideMenu().
        if (isOpen && !IsMenuVisible)
        {
            CloseAndRestorePreviousState();
            return;
        }

   
        // Keyboard shortcuts while the menu is open: 1 / 2 / 3 pick a card.
        if (!IsOpen || currentChoices.Count == 0) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        for (int i = 0; i < currentChoices.Count && i < SelectKeys.Length; i++)
        {
            if (kb[SelectKeys[i]].wasPressedThisFrame)
            {
                Choose(currentChoices[i]);
                return;
            }
        }
    }

    /// Show the menu with buff choices. Called DIRECTLY by
    /// BuffSystem.TriggerBuffSelection() — not via an event — so it still works
    /// even while this object is inactive at game start.
    public void ShowChoices(List<BuffOption> choices)
    {
        if (IsOpen || choices == null || choices.Count == 0) return;

        DiscoverCardReferences();
        currentChoices.Clear();
        currentChoices.AddRange(choices);
        PopulateCards();

        if (pauseGameWhileOpen)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (showCursorWhileOpen)
        {
            previousCursorVisible = Cursor.visible;
            Cursor.visible = true;
        }

        isOpen = true;
        SetVisible(true);
    }

    // Close the menu & resume the game. Called by BuffSystem via SelectBuff().
    public void HideMenu()
    {
        CloseAndRestorePreviousState();
        SetVisible(false);
    }

    private void CloseAndRestorePreviousState()
    {
        bool wasOpen = isOpen;
        isOpen = false;
        currentChoices.Clear();

        if (wasOpen && pauseGameWhileOpen)
        {
            Time.timeScale = previousTimeScale;
        }

        if (wasOpen && showCursorWhileOpen)
        {
            Cursor.visible = previousCursorVisible;
        }

        previousTimeScale = 1f;
        previousCursorVisible = false;
    }


    // Pick a buff
    private void Choose(BuffOption option)
    {
        if (!IsOpen || option == null) return;

        // Find BuffSystem with a fallback — if Instance is null (e.g. BuffManager
        // was just added / Awake order differs), search the scene directly.
        BuffSystem buffSystem = BuffSystem.Instance != null
            ? BuffSystem.Instance
            : FindAnyObjectByType<BuffSystem>(FindObjectsInactive.Include);

        if (buffSystem == null)
        {
            Debug.LogWarning("[BuffSelectionUI] BuffSystem not found in the scene — the buff cannot be applied! " +
                             "Add a BuffSystem component (e.g. object 'BuffManager').");
            return;
        }

        Debug.Log($"[BuffSelectionUI] Buff picked: {option.title} ({option.description})");
        // SelectBuff applies the buff stats and then calls HideMenu() back on this script.
        buffSystem.SelectBuff(option);
    }

    //populate cards
    /// Fill each card slot with buff data; surplus slots are hidden.
    private void PopulateCards()
    {
        if (cards == null) return;

        for (int i = 0; i < cards.Length; i++)
        {
            BuffCardSlot slot = cards[i];
            if (slot == null || !slot.IsAssigned) continue;

            bool hasChoice = i < currentChoices.Count;

            // Hide surplus card slots
            if (slot.root != null) slot.root.SetActive(hasChoice);

            if (!hasChoice)
            {
                // Clean up the button listeners of hidden cards
                if (slot.button != null) slot.button.onClick.RemoveAllListeners();
                continue;
            }

            BuffOption option = currentChoices[i];

            BindCardVisual(slot, option);

            // Category label + card color based on the buff type
            if (slot.categoryLabel != null)
            {
                slot.categoryLabel.text = GetCategoryLabel(option.category);
                slot.categoryLabel.color = GetCategoryLabelColor(option.category);
            }
            if (tintCardsByCategory && slot.root != null)
            {
                Image cardImage = slot.root.GetComponent<Image>();
                if (cardImage != null) cardImage.color = GetCategoryCardColor(option.category);
            }

            if (slot.titleText != null) slot.titleText.text = option.title;
            if (slot.descriptionText != null) slot.descriptionText.text = option.description;
            string statPreview = BuffSystem.Instance != null
                ? BuffSystem.Instance.GetStatPreviewText(option)
                : string.Empty;
            if (slot.statPreviewText != null)
            {
                slot.statPreviewText.text = statPreview;
            }
            if (slot.button != null)
            {
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => Choose(option));
            }
        }
    }

    // helper

    private static string GetCategoryLabel(BuffCategory category)
    {
        switch (category)
        {
            case BuffCategory.Character: return "[ CHARACTER ]";
            case BuffCategory.Gun:       return "[ GUN ]";
            default: return string.Empty;
        }
    }

    private Color GetCategoryLabelColor(BuffCategory category)
    {
        switch (category)
        {
            case BuffCategory.Character: return new Color(0.55f, 0.85f, 0.55f);
            case BuffCategory.Gun:       return new Color(0.55f, 0.65f, 0.95f);
            default: return Color.white;
        }
    }

    private Color GetCategoryCardColor(BuffCategory category)
    {
        switch (category)
        {
            case BuffCategory.Character: return characterCardColor;
            case BuffCategory.Gun:       return gunCardColor;
            default: return Color.white;
        }
    }

    private void SetVisible(bool visible)
    {
        if (menuRoot != null) menuRoot.SetActive(visible);
    }

    /// Maps the existing card prefab hierarchy to BuffCardSlot fields.
    /// The current cards use TMP text and have this structure:
    /// CardRoot/NameHolder/BuffName, CardRoot/DescriptionHolder/Description,
    /// and CardRoot/Choose (Button). Category and stat-preview labels are optional.
    private void DiscoverCardReferences()
    {
        if (!autoDiscoverCardReferences || menuRoot == null) return;

        List<BuffCardSlot> discovered = new List<BuffCardSlot>();
        Transform menuTransform = menuRoot.transform;

        for (int i = 0; i < menuTransform.childCount; i++)
        {
            Transform cardTransform = menuTransform.GetChild(i);
            if (!cardTransform.gameObject.activeSelf) continue;

            Button cardButton = FindButton(cardTransform);

            // Ignore layout/helper objects that are not actual cards.
            if (cardButton == null) continue;

            BuffCardSlot slot = new BuffCardSlot
            {
                root = cardTransform.gameObject,
                button = cardButton,
                titleText = FindText(cardTransform, "NameHolder/BuffName") ?? FindText(cardTransform, "BuffName"),
                descriptionText = FindText(cardTransform, "DescriptionHolder/Description") ?? FindText(cardTransform, "Description"),
                categoryLabel = FindText(cardTransform, "CategoryLabel"),
                statPreviewText = FindText(cardTransform, "StatPreview")
            };

            slot.configuredRoot = slot.root;

            discovered.Add(slot);
        }

        if (discovered.Count == 0) return;

        // Preserve explicit Inspector assignments, but fill missing references
        // from the detected prefab hierarchy.
        if (cards == null || cards.Length != discovered.Count)
        {
            cards = discovered.ToArray();
            return;
        }

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
            {
                cards[i] = discovered[i];
                continue;
            }

            BuffCardSlot detected = discovered[i];
            cards[i].root ??= detected.root;
            cards[i].button ??= detected.button;
            cards[i].titleText ??= detected.titleText;
            cards[i].descriptionText ??= detected.descriptionText;
            cards[i].categoryLabel ??= detected.categoryLabel;
            cards[i].statPreviewText ??= detected.statPreviewText;
        }
    }

    private static Button FindButton(Transform root)
    {
        if (root == null) return null;

        Transform choose = FindDescendant(root, "Choose");
        if (choose != null && choose.TryGetComponent(out Button chooseButton)) return chooseButton;

        return root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true);
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform textTransform = FindDescendant(root, path);
        return textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindDescendant(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;

        Transform current = root;
        string[] parts = path.Split('/');
        foreach (string part in parts)
        {
            current = current.Find(part);
            if (current == null) return null;
        }

        return current;
    }

    private void BindCardVisual(BuffCardSlot slot, BuffOption option)
    {
        if (slot == null) return;

        GameObject desiredPrefab = usePrebuiltCardPrefabs && option != null ? option.cardPrefab : null;

        if (desiredPrefab != null && slot.boundPrefab != desiredPrefab)
        {
            int siblingIndex = slot.configuredRoot != null
                ? slot.configuredRoot.transform.GetSiblingIndex()
                : menuRoot.transform.childCount - 1;

            if (slot.runtimeRoot != null) Destroy(slot.runtimeRoot);
            if (slot.configuredRoot != null) slot.configuredRoot.SetActive(false);

            GameObject visual = Instantiate(desiredPrefab, menuRoot.transform, false);
            visual.name = $"{desiredPrefab.name}_RuntimeCard";
            CopyLayout(slot.configuredRoot, visual);
            visual.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, menuRoot.transform.childCount - 1));

            slot.runtimeRoot = visual;
            slot.boundPrefab = desiredPrefab;
            slot.root = visual;
            slot.button = FindButton(visual.transform);
            slot.titleText = FindText(visual.transform, "NameHolder/BuffName") ?? FindText(visual.transform, "BuffName");
            slot.descriptionText = FindText(visual.transform, "DescriptionHolder/Description") ?? FindText(visual.transform, "Description");
            slot.categoryLabel = FindText(visual.transform, "CategoryLabel");
            slot.statPreviewText = FindText(visual.transform, "StatPreview");
        }
        else if (desiredPrefab == null && slot.runtimeRoot != null)
        {
            Destroy(slot.runtimeRoot);
            slot.runtimeRoot = null;
            slot.boundPrefab = null;
            slot.root = slot.configuredRoot;
            if (slot.configuredRoot != null) slot.configuredRoot.SetActive(true);
            slot.button = FindButton(slot.root != null ? slot.root.transform : null);
            slot.titleText = FindText(slot.root != null ? slot.root.transform : null, "NameHolder/BuffName")
                ?? FindText(slot.root != null ? slot.root.transform : null, "BuffName");
            slot.descriptionText = FindText(slot.root != null ? slot.root.transform : null, "DescriptionHolder/Description")
                ?? FindText(slot.root != null ? slot.root.transform : null, "Description");
        }

        if (slot.root != null) slot.root.SetActive(true);
    }

    private static void CopyLayout(GameObject source, GameObject destination)
    {
        if (source == null || destination == null) return;

        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform destinationRect = destination.GetComponent<RectTransform>();
        if (sourceRect == null || destinationRect == null) return;

        destinationRect.anchorMin = sourceRect.anchorMin;
        destinationRect.anchorMax = sourceRect.anchorMax;
        destinationRect.anchoredPosition = sourceRect.anchoredPosition;
        destinationRect.sizeDelta = sourceRect.sizeDelta;
        destinationRect.pivot = sourceRect.pivot;
        destinationRect.localRotation = sourceRect.localRotation;
        destinationRect.localScale = sourceRect.localScale;
    }
}
