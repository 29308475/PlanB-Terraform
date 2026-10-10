using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PlanBProductionChains
{
    [BepInPlugin("tony.planb.productionchains", "Produktionsketten", "0.6.6")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin instance;
        private readonly RecipeCatalog catalog = new RecipeCatalog();
        private readonly Dictionary<CItem, int> choices = new Dictionary<CItem, int>();
        private readonly HashSet<string> collapsed = new HashSet<string>();
        private GameObject canvasObject, panel;
        private RectTransform productList, details;
        private InputField search;
        private Font font;
        private CItem selected;
        private GameObject launcherObject;
        private RectTransform repeatBuildButton;
        private ConfigEntry<string> lastBuildGroup;
        private GameObject productPane, detailPane;
        private Text heading;
        private RectTransform backButton;
        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private bool showBuildings;
        private RectTransform goodsTab, buildingsTab;
        private GameObject treeToolbar;
        private Dropdown routeProductDropdown, routeDropdown;
        private CItem routeTarget;
        private List<CItem> routeProducts = new List<CItem>();
        private float treeRootX;
        private ChainBlueprint activeBlueprint;
        private Harmony harmony;
        private bool hudActive;
        private static readonly Color Background = new Color(.90f, .89f, .95f, 1f);
        private static readonly Color RowColor = Color.white;
        private static readonly Color Ink = new Color(.19f, .20f, .27f);
        private static readonly Color Purple = new Color32(121, 125, 245, 255);
        private static readonly Color Border = new Color32(183, 185, 245, 255);

        private void Awake()
        {
            instance = this;
            lastBuildGroup = Config.Bind("Baugruppen", "LetzteBaugruppe", "", "Zuletzt ausgewählte Baugruppe einschließlich Herstellungswegen.");
            harmony = new Harmony("tony.planb.productionchains");
            harmony.PatchAll(typeof(Plugin));
            harmony.PatchAll(typeof(ModsMenuIntegration));
            Logger.LogInfo("Produktionsketten 0.6.6 geladen. Öffnen neben dem oberen Info-Button.");
        }

        [HarmonyPatch(typeof(SSceneHud), "OnActivate"), HarmonyPostfix]
        private static void HudActivated() { if (instance != null) instance.hudActive = true; }

        [HarmonyPatch(typeof(SSceneHud), "OnDeactivate"), HarmonyPostfix]
        private static void HudDeactivated()
        {
            if (instance == null) return;
            instance.hudActive = false;
            instance.DisposeUI();
        }

        private void Update()
        {
            if (!hudActive) return;
            if (canvasObject == null)
            {
                try { BuildUI(); }
                catch (Exception error) { Logger.LogError(error); hudActive = false; DisposeUI(); }
            }
            if (panel == null || (search != null && search.isFocused)) return;
            if (Input.GetKeyDown(KeyCode.Escape) && panel.activeSelf) panel.SetActive(false);
        }

        private void Toggle()
        {
            if (!panel.activeSelf)
            {
                catalog.Load();
                choices.Clear();
                collapsed.Clear();
                panel.SetActive(true);
                ShowProducts();
                RefreshProducts();
                Logger.LogInfo("Rezeptübersicht: " + catalog.Routes.Count + " Produkte.");
            }
            else panel.SetActive(false);
        }

        private void BuildUI()
        {
            font = SSceneSingleton<SSceneHud>.Inst.textTechTitle?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateRoundedSprite();
            canvasObject = new GameObject("PlanBProductionChains", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;
            panel = Box(root, "ProduktionskettenFenster", Color.white).gameObject;
            var window = (RectTransform)panel.transform;
            window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
            window.pivot = new Vector2(.5f, .5f);
            window.sizeDelta = new Vector2(1040, 610);
            window.anchoredPosition = Vector2.zero;
            var surface = Box(window, "Fensterfläche", Background); Stretch(surface, 6);
            var titlebar = Box(window, "Titelleiste", Purple); Place(titlebar, 7, 7, 1026, 28);
            heading = Label(titlebar, "PRODUKTIONSKETTEN", 18);
            heading.color = Color.white; heading.alignment = TextAnchor.MiddleCenter;
            Stretch(heading.rectTransform, 0); heading.rectTransform.offsetMin = new Vector2(130, 0); heading.rectTransform.offsetMax = new Vector2(-130, 0);
            var close = Button(titlebar, "×", () => panel.SetActive(false));
            Place(close, 998, 4, 20, 20);
            close.GetComponent<Image>().color = Background;
            var closeText = close.GetComponentInChildren<Text>(); closeText.color = Purple;
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); closeText.fontSize = 20;
            closeText.alignment = TextAnchor.MiddleCenter; Stretch(closeText.rectTransform, 0);
            backButton = Button(titlebar, "‹ PRODUKTE", ShowProducts); Place(backButton, 8, 3, 100, 22);
            backButton.GetComponent<Image>().color = Purple;
            backButton.GetComponentInChildren<Text>().color = Color.white;
            var field = Box(window, "Suche", Color.white);
            Place(field, 14, 43, 280, 28);
            search = field.gameObject.AddComponent<InputField>();
            var inputText = Label(field, "", 12); Stretch(inputText.rectTransform, 5);
            var placeholder = Label(field, "Produkt suchen …", 12); Stretch(placeholder.rectTransform, 5);
            placeholder.color = new Color(.48f, .48f, .58f);
            search.textComponent = inputText; search.placeholder = placeholder;
            search.onValueChanged.AddListener(_ => RefreshProducts());
            goodsTab = Button(window, "WAREN", () => { showBuildings = false; RefreshProducts(); });
            Place(goodsTab, 308, 43, 92, 28);
            buildingsTab = Button(window, "GEBÄUDE", () => { showBuildings = true; RefreshProducts(); });
            Place(buildingsTab, 406, 43, 108, 28);
            var hint = Label(window, "Produkt auswählen · Mengen je Rezeptdurchlauf", 12);
            Place(hint.rectTransform, 534, 43, 476, 28);
            productList = Scroll(window, "Produkte", 14, 80, 1012, 516, true);
            details = Scroll(window, "Rezeptdetails", 14, 80, 1012, 516);
            productPane = productList.parent.parent.gameObject;
            detailPane = details.parent.parent.gameObject;
            // The tree uses explicit node coordinates instead of a vertical text layout.
            details.GetComponent<VerticalLayoutGroup>().enabled = false;
            details.GetComponent<ContentSizeFitter>().enabled = false;
            details.parent.parent.GetComponent<ScrollRect>().horizontal = false;
            details.parent.parent.GetComponent<ScrollRect>().vertical = false;
            var toolbar = Box(window, "Herstellungswege", Background);
            Place(toolbar, 14, 43, 1012, 46); treeToolbar = toolbar.gameObject;
            var productLabel = Label(toolbar, "Produkt", 11);
            Place(productLabel.rectTransform, 4, 0, 206, 16);
            var routeLabel = Label(toolbar, "Herstellungsweg", 11);
            Place(routeLabel.rectTransform, 222, 0, 790, 16);
            routeProductDropdown = CreateDropdown(toolbar, 0, 210);
            routeDropdown = CreateDropdown(toolbar, 218, 794);
            ShowProducts();
            panel.SetActive(false);
            BuildHudButton();
        }

        private void BuildHudButton()
        {
            var info = SSceneSingleton<SSceneHud>.Inst.btTechConditionInfos;
            if (info == null) throw new InvalidOperationException("Info-Button im HUD nicht gefunden.");
            var source = (RectTransform)info.transform;
            var parent = canvasObject.transform;
            launcherObject = Instantiate(info.gameObject, parent, false);
            launcherObject.name = "ProduktionskettenButton";
            var rect = (RectTransform)launcherObject.transform;
            var button = launcherObject.GetComponent<Button>();
            // Native transitions must not tint our background on top of an already
            // tinted palette color, or retain an animation state from the clone.
            foreach (var animator in launcherObject.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            // Replace the serialized Info action as well as runtime listeners.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Toggle);
            // The native button contains a circular graphic in addition to its rectangular
            // background. Replace the copy's graphics explicitly to keep the intended outline.
            foreach (var graphic in launcherObject.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            var nativeTint = info.transition == Selectable.Transition.ColorTint;
            var background = Box(rect, "RechteckigerHintergrund", nativeTint && info.targetGraphic != null ? info.targetGraphic.color : Color.white);
            Stretch(background, 0);
            MatchHudButtonShape(background.GetComponent<Image>(), info);
            AddHudButtonBorder(background);
            background.SetAsFirstSibling();
            button.targetGraphic = background.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            if (nativeTint) button.colors = info.colors;
            else
            {
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = Border;
                colors.highlightedColor = Purple;
                colors.pressedColor = Purple;
                colors.selectedColor = Border;
                colors.disabledColor = new Color(Border.r, Border.g, Border.b, .5f);
                button.colors = colors;
            }
            // ColorTint lives on the CanvasRenderer, separately from Image.color.
            // Set the initial normal state explicitly before the first pointer event.
            background.GetComponent<Image>().canvasRenderer.SetColor(button.colors.normalColor * button.colors.colorMultiplier);
            Logger.LogInfo("Kettenbutton: native Transition=" + info.transition + ", Grundfarbe=" + background.GetComponent<Image>().color + ", Normal=" + button.colors.normalColor + ", Hover=" + button.colors.highlightedColor);
            launcherObject.SetActive(true);
            var tooltip = launcherObject.GetComponent<CTooltipTarget>() ?? launcherObject.AddComponent<CTooltipTarget>();
            tooltip.text = "Produktionsketten";
            tooltip.textDesc = "Gebäude, Zutaten und Vorprodukte für ein Produkt ansehen.";

            var sizing = rect.GetComponent<LayoutElement>();
            if (sizing != null) sizing.ignoreLayout = true;
            var fitter = rect.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            PositionHudButton();

            var icon = new GameObject("KettenIcon", typeof(RectTransform), typeof(ChainIcon));
            icon.transform.SetParent(rect, false);
            Stretch((RectTransform)icon.transform, 5);
            icon.GetComponent<ChainIcon>().color = Color.white;
            icon.GetComponent<ChainIcon>().raycastTarget = false;
            repeatBuildButton = Button(canvasObject.transform, "", RepeatLastBuild);
            repeatBuildButton.name = "LetzteBaugruppeButton";
            MatchHudButtonShape(repeatBuildButton.GetComponent<Image>(), info);
            AddHudButtonBorder(repeatBuildButton);
            var repeatButton = repeatBuildButton.GetComponent<Button>();
            repeatButton.colors = button.colors;
            repeatBuildButton.GetComponent<Image>().color = background.GetComponent<Image>().color;
            repeatBuildButton.GetComponent<Image>().canvasRenderer.SetColor(button.colors.normalColor * button.colors.colorMultiplier);
            var repeatLabel = repeatBuildButton.GetComponentInChildren<Text>();
            repeatLabel.gameObject.SetActive(false);
            var buildIcon = new GameObject("BaugruppeWiederholenIcon", typeof(RectTransform), typeof(BuildIcon));
            buildIcon.transform.SetParent(repeatBuildButton, false);
            Stretch((RectTransform)buildIcon.transform, 5);
            buildIcon.GetComponent<BuildIcon>().color = Color.white;
            buildIcon.GetComponent<BuildIcon>().raycastTarget = false;
            var repeatTooltip = repeatBuildButton.gameObject.AddComponent<CTooltipTarget>();
            repeatTooltip.text = "Letzte Baugruppe bauen";
            repeatTooltip.textDesc = "Wählt die zuletzt verwendete Baugruppe erneut als gemeinsame Bauvorschau aus.";
            repeatButton.interactable = !string.IsNullOrEmpty(lastBuildGroup.Value);
            PositionHudButton();
        }

        private void RefreshProducts()
        {
            Clear(productList);
            var query = search.text.Trim();
            goodsTab.GetComponent<Image>().color = showBuildings ? Color.white : Border;
            buildingsTab.GetComponent<Image>().color = showBuildings ? Border : Color.white;
            var matches = catalog.Products.Where(x => IsBuilding(x) == showBuildings && RecipeCatalog.Name(x).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            foreach (var group in matches.GroupBy(x => GroupName(x)).OrderBy(x => GroupOrder(x.Key)))
            {
                var category = Line(productList, group.Key, 15, 23); category.fontStyle = FontStyle.Bold;
                var products = group.ToList();
                for (var offset = 0; offset < products.Count; offset += 6)
                {
                    var rowObject = new GameObject("Produktreihe", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                    rowObject.transform.SetParent(productList, false);
                    var row = (RectTransform)rowObject.transform; Height(row, 48);
                    var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
                    layout.spacing = 5; layout.childControlWidth = true; layout.childControlHeight = true;
                    layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
                    for (var i = offset; i < Math.Min(offset + 6, products.Count); i++)
                    {
                        var product = products[i];
                        var border = Box(row, "Produktkarte", Border);
                        var sizing = border.gameObject.AddComponent<LayoutElement>(); sizing.preferredWidth = 159; sizing.minWidth = 159;
                        var button = Button(border, RecipeCatalog.Name(product), () =>
                        { SelectProduct(product); });
                        Stretch(button, 3);
                        var label = button.GetComponentInChildren<Text>(); label.fontStyle = FontStyle.Bold;
                        label.fontSize = 12; label.resizeTextForBestFit = true;
                        label.resizeTextMinSize = 9; label.resizeTextMaxSize = 12;
                        AddIcon(button, product, 36);
                    }
                }
            }
            if (matches.Count == 0) Line(productList, "Keine passenden Produkte.");
            ResetScroll(productList);
        }

        private void MatchHudButtonShape(Image image, Button info)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.fillCenter = true;
        }

        private static void SetHudCornerRadius(Image image, Vector2 size)
        {
            if (image == null || image.sprite == null || image.canvas == null) return;
            // Sliced corners depend on the canvas pixel density. Define their
            // displayed radius explicitly, independently of native sprite scaling.
            var radius = Mathf.Min(size.x, size.y) * .22f;
            var pixelsPerUnit = image.sprite.pixelsPerUnit / image.canvas.referencePixelsPerUnit;
            image.pixelsPerUnitMultiplier = image.sprite.border.x / (radius * pixelsPerUnit);
        }
        private static void AddHudButtonBorder(RectTransform surface)
        {
            var outline = surface.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);
            outline.useGraphicAlpha = false;
        }

        private void LateUpdate()
        {
            if (hudActive && launcherObject != null) PositionHudButton();
        }

        private void PositionHudButton()
        {
            var info = SSceneSingleton<SSceneHud>.Inst.btTechConditionInfos;
            if (info == null || launcherObject == null) return;
            var source = (RectTransform)info.transform;
            var nativeCanvas = source.GetComponentInParent<Canvas>();
            var camera = nativeCanvas != null && nativeCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? nativeCanvas.worldCamera : null;
            var corners = new Vector3[4]; source.GetWorldCorners(corners);
            var root = (RectTransform)canvasObject.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[0]), null, out var lowerLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[2]), null, out var upperRight);
            var size = upperRight - lowerLeft;
            if (size.x <= 0 || size.y <= 0) return;
            var rect = (RectTransform)launcherObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = size;
            SetHudCornerRadius(launcherObject.GetComponent<Button>().targetGraphic as Image, size);
            // Separate canvas: no native layout can move this copy over the original Info button.
            rect.anchoredPosition = new Vector2(upperRight.x + 6 + size.x * .5f, (lowerLeft.y + upperRight.y) * .5f);
            if (repeatBuildButton != null)
            {
                repeatBuildButton.anchorMin = repeatBuildButton.anchorMax = repeatBuildButton.pivot = new Vector2(1, 0);
                repeatBuildButton.sizeDelta = size;
                SetHudCornerRadius(repeatBuildButton.GetComponent<Image>(), size);
                repeatBuildButton.anchoredPosition = new Vector2(-14, 30);
                var selectionScene = SSceneSingleton<SSceneHud_Selection>.Inst;
                var selectionPanel = selectionScene == null ? null : selectionScene.uiSelection.rootPanel;
                if (selectionPanel != null && selectionPanel.activeInHierarchy && selectionPanel.transform is RectTransform popup)
                {
                    var popupCanvas = popup.GetComponentInParent<Canvas>();
                    var popupCamera = popupCanvas != null && popupCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? popupCanvas.worldCamera : null;
                    popup.GetWorldCorners(corners);
                    var left = float.PositiveInfinity;
                    foreach (var corner in corners)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(popupCamera, corner), null, out var position);
                        left = Mathf.Min(left, position.x);
                    }
                    var x = Mathf.Min(-14, left - root.rect.xMax - 10);
                    repeatBuildButton.anchoredPosition = new Vector2(Mathf.Max(-root.rect.width + size.x + 14, x), 30);
                }
            }
        }

        private static string GroupName(CItem item)
        {
            switch (item.uiGroup?.codeName)
            {
                case "minerals": case "resources": return "BODENSCHÄTZE UND ROHSTOFFE";
                case "construction": return "BAUMATERIALIEN";
                case "tech": return "TECHNISCHE MATERIALIEN";
                case "organic": return "BIOLOGISCHE PRODUKTE";
                case "greenhousegas": return "TERRAFORMING";
                default: return item is CItem_Content || item is CItem_Vehicle || item is CItem_Way ? "GEBÄUDE UND TRANSPORT" : "WEITERE PRODUKTE";
            }
        }
        private static bool IsBuilding(CItem item) => item is CItem_Content || item is CItem_Vehicle || item is CItem_Way;
        private static int GroupOrder(string name)
        { return name.StartsWith("BODEN") ? 0 : name.StartsWith("BAU") ? 1 : name.StartsWith("TECH") ? 2 : name.StartsWith("BIO") ? 3 : name.StartsWith("TERRA") ? 4 : name.StartsWith("GEB") ? 5 : 6; }
        private void ShowProducts()
        {
            productPane.SetActive(true); detailPane.SetActive(false); backButton.gameObject.SetActive(false);
            search.gameObject.SetActive(true); heading.text = "PRODUKTIONSKETTEN";
            goodsTab.gameObject.SetActive(true); buildingsTab.gameObject.SetActive(true);
            treeToolbar.SetActive(false);
        }
        private void ShowDetails()
        {
            productPane.SetActive(false); detailPane.SetActive(true); backButton.gameObject.SetActive(true);
            search.gameObject.SetActive(false); heading.text = RecipeCatalog.Name(selected);
            goodsTab.gameObject.SetActive(false); buildingsTab.gameObject.SetActive(false);
            treeToolbar.SetActive(true);
            ResetScroll(details);
        }
        private void SelectProduct(CItem product)
        {
            try
            {
                selected = product; routeTarget = product; collapsed.Clear(); ShowDetails(); RefreshDetails();
                Canvas.ForceUpdateCanvases(); ResetScroll(details);
                Logger.LogInfo("Produktkette geöffnet: " + product.codeName + "; Fenster sichtbar: " + panel.activeInHierarchy);
            }
            catch (Exception error)
            {
                Logger.LogError(error);
                panel.SetActive(true); detailPane.SetActive(true);
                Line(details, "Die Produktkette konnte nicht vollständig angezeigt werden.");
            }
        }
        private void ResetScroll(RectTransform content)
        {
            var scroll = content.parent.parent.GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 1;
            if (scroll.horizontal)
            {
                var overflow = content.rect.width - scroll.viewport.rect.width;
                scroll.horizontalNormalizedPosition = overflow > 0 ? Mathf.Clamp01((treeRootX - scroll.viewport.rect.width / 2) / overflow) : 0;
            }
        }

        private void RefreshDetails()
        {
            Clear(details);
            if (selected == null || !catalog.Routes.ContainsKey(selected))
            {
                Line(details, "Wähle links ein Produkt aus.", 15);
                return;
            }
            collapsed.Clear();
            var tree = ProductionTree.Build(selected, catalog, choices, collapsed);
            RefreshRouteToolbar(tree.Root);
            Canvas.ForceUpdateCanvases();
            var viewport = (RectTransform)details.parent;
            var scale = Mathf.Min(1f, Mathf.Min((viewport.rect.width - 16) / tree.Width, (viewport.rect.height - 16) / tree.Height));
            details.anchorMin = details.anchorMax = new Vector2(0, 1);
            details.pivot = new Vector2(0, 1);
            details.localScale = Vector3.one;
            details.sizeDelta = viewport.rect.size;
            details.anchoredPosition = Vector2.zero;
            var graphObject = new GameObject("Produktionsbaum", typeof(RectTransform));
            graphObject.transform.SetParent(details, false);
            var graph = (RectTransform)graphObject.transform;
            Place(graph, (viewport.rect.width - tree.Width * scale) / 2, 8, tree.Width, tree.Height);
            graph.localScale = new Vector3(scale, scale, 1);
            treeRootX = graph.anchoredPosition.x + tree.Root.X * scale;
            DrawTreeConnections(graph, tree.Root);
            DrawTreeNode(graph, tree.Root);
            ResetScroll(details);
        }

        private void DrawTreeConnections(RectTransform graph, ProductionNode node)
        {
            var bottom = node.Depth * ProductionTree.Row + 4 + ProductionTree.CardHeight;
            var fork = bottom + 10;
            foreach (var child in node.Children)
            {
                TreeLine(graph, node.X, bottom, node.X, fork);
                TreeLine(graph, node.X, fork, child.X, fork);
                TreeLine(graph, child.X, fork, child.X, child.Depth * ProductionTree.Row + 4);
                DrawTreeConnections(graph, child);
            }
        }
        private void TreeLine(Transform graph, float x1, float y1, float x2, float y2)
        {
            var line = Box(graph, "Verbindung", Purple);
            line.GetComponent<Image>().raycastTarget = false;
            Place(line, Mathf.Min(x1, x2) - 1, Mathf.Min(y1, y2) - 1, Mathf.Max(2, Mathf.Abs(x2 - x1)), Mathf.Max(2, Mathf.Abs(y2 - y1)));
        }
        private void DrawTreeNode(RectTransform graph, ProductionNode node)
        {
            var outer = Box(graph, "Produktknoten", Border);
            Place(outer, node.X - ProductionTree.CardWidth / 2, node.Depth * ProductionTree.Row + 4, ProductionTree.CardWidth, ProductionTree.CardHeight);
            var card = Box(outer, "Karte", Color.white); Stretch(card, 2);
            var name = Box(card, "Produktname", Color.white);
            var productName = Label(name, RecipeCatalog.Name(node.Product), 12);
            Stretch(productName.rectTransform, 2);
            Place(name, 4, 4, 168, 24); AddIcon(name, node.Product, 22);
            var title = name.GetComponentInChildren<Text>(); title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 10; title.resizeTextMaxSize = 12;
            var amount = node.Amount > 0 ? "Bedarf: " + node.Amount.ToString("0.###", CultureInfo.CurrentCulture) + " je Rezept" : "Endprodukt";
            var amountLabel = Label(card, amount, 9); amountLabel.color = new Color(.43f, .43f, .52f);
            Place(amountLabel.rectTransform, 6, 30, 164, 14);
            if (node.IsCycle)
            { Place(Label(card, "Kreislauf / Rückgewinnung", 11).rectTransform, 6, 46, 164, 38); }
            else if (node.Route != null)
            {
                var buildingLabel = Label(card, RecipeCatalog.Name(node.Route.Building) + (node.Route.IsUnlocked ? "" : " · gesperrt"), 11);
                Place(buildingLabel.rectTransform, 6, 46, 164, 18);
                buildingLabel.resizeTextForBestFit = true; buildingLabel.resizeTextMinSize = 9; buildingLabel.resizeTextMaxSize = 11;
                var resultLabel = Label(card, "Ergebnis: " + string.Join(" + ", node.Route.Outputs.Select(Format)), 9);
                Place(resultLabel.rectTransform, 6, 66, 164, 18);
                resultLabel.resizeTextForBestFit = true; resultLabel.resizeTextMinSize = 8; resultLabel.resizeTextMaxSize = 9;
                var note = node.Route.Inputs.Count == 0 ? "Gewinnung aus der Umgebung" : "Zutaten: siehe verbundene Knoten";
                var buildButton = Button(card, "Bauen", () => PrepareChainBuild(node));
                Place(buildButton, 6, 86, 164, 20);
                buildButton.GetComponentInChildren<Text>().fontSize = 11;
                buildButton.GetComponent<Image>().color = Border;
                var tooltip = card.gameObject.AddComponent<CTooltipTarget>();
                tooltip.text = RecipeCatalog.Name(node.Product); tooltip.textDesc = node.Route.Note ?? note;
            }
            else Place(Label(card, "Keine Herstellungsquelle bekannt", 11).rectTransform, 6, 46, 164, 38);
            foreach (var child in node.Children) DrawTreeNode(graph, child);
        }

        private void PrepareChainBuild(ProductionNode node)
        {
            try
            {
                var blueprint = ChainBlueprint.Create(node, true);
                lastBuildGroup.Value = BuildGroupMemory.Capture(node);
                if (repeatBuildButton != null)
                {
                    repeatBuildButton.GetComponent<Button>().interactable = true;
                    repeatBuildButton.GetComponent<CTooltipTarget>().textDesc = RecipeCatalog.Name(node.Product) +
                        " · " + blueprint.Items.Count + " Gebäude. Erneut als gemeinsame Bauvorschau auswählen.";
                }
                panel.SetActive(false);
                StartCoroutine(StartBlueprintAfterClick(blueprint));
            }
            catch (Exception error)
            { Logger.LogWarning(error.Message); SSceneSingleton<SSceneUIOverlay>.Inst.ShowMessage(error.Message); }
        }
        private void RepeatLastBuild()
        {
            try
            {
                catalog.Load();
                PrepareChainBuild(BuildGroupMemory.Restore(lastBuildGroup.Value, catalog));
            }
            catch (Exception error)
            { Logger.LogWarning(error.Message); SSceneSingleton<SSceneUIOverlay>.Inst.ShowMessage(error.Message); }
        }
        private IEnumerator StartBlueprintAfterClick(ChainBlueprint blueprint)
        {
            // The UI click must never also place the new group on the planet.
            yield return null;
            while (Input.GetMouseButton(0)) yield return null;
            if (!hudActive) yield break;
            activeBlueprint = blueprint;
            blueprint.Begin();
            SSceneSingleton<SSceneUIOverlay>.Inst.ShowMessage("Baupaket: " + blueprint.Items.Count + " Gebäude. " +
                (blueprint.SeparateExtractors > 0 ? "Separat am passenden Standort: " + blueprint.SeparateStages + ". " : "") +
                (blueprint.VariableYields ? "Landwirtschaft: Ertrag standortabhängig, ein Feld je Farm. " : "") +
                "Linksklick baut, Rechtsklick bricht ab.");
        }

        [HarmonyPatch(typeof(CItem_Content), nameof(CItem_Content.Build_IFP)), HarmonyPrefix]
        private static bool ValidateChainPlacement(ref bool __result)
        {
            var blueprint = instance?.activeBlueprint;
            if (blueprint == null || !blueprint.MatchesCurrentSelection() || blueprint.CanBuild()) return true;
            blueprint.ShowError(); __result = false; return false;
        }
        [HarmonyPatch(typeof(CItem), nameof(CItem.GenerateItemBuildingMesh)), HarmonyPrefix]
        private static void ShowChainValidity(ref bool isValid)
        {
            var blueprint = instance?.activeBlueprint;
            if (blueprint != null && blueprint.MatchesCurrentSelection() && !blueprint.CanBuild()) isValid = false;
        }

        private void RefreshRouteToolbar(ProductionNode root)
        {
            var nodes = new List<ProductionNode>(); CollectNodes(root, nodes);
            routeProducts = nodes.Where(x => catalog.HasProductionAlternatives(x.Product))
                .Select(x => x.Product).Distinct().ToList();
            treeToolbar.SetActive(routeProducts.Count > 0);
            Place((RectTransform)detailPane.transform, 14, routeProducts.Count > 0 ? 98 : 80, 1012, routeProducts.Count > 0 ? 498 : 516);
            if (routeProducts.Count == 0) return;
            if (!routeProducts.Contains(routeTarget)) routeTarget = routeProducts[0];
            routeProductDropdown.onValueChanged = new Dropdown.DropdownEvent();
            routeProductDropdown.ClearOptions(); routeProductDropdown.AddOptions(routeProducts.Select(RecipeCatalog.Name).ToList());
            routeProductDropdown.SetValueWithoutNotify(routeProducts.IndexOf(routeTarget));
            routeProductDropdown.onValueChanged.AddListener(index => { routeTarget = routeProducts[index]; RefreshRouteToolbar(root); });
            var options = catalog.Routes[routeTarget];
            var chosen = choices.TryGetValue(routeTarget, out var value) ? value : catalog.DefaultRoute(routeTarget);
            routeDropdown.onValueChanged = new Dropdown.DropdownEvent(); routeDropdown.ClearOptions();
            routeDropdown.AddOptions(options.Select(x => RecipeCatalog.Name(x.Building) + " · " + (x.Inputs.Count == 0 ? "Gewinnung" : string.Join(" + ", x.Inputs.Select(Format))) + (x.IsUnlocked ? "" : " · gesperrt")).ToList());
            routeDropdown.SetValueWithoutNotify(chosen); routeDropdown.interactable = options.Count > 1;
            routeDropdown.onValueChanged.AddListener(index => { choices[routeTarget] = index; RefreshDetails(); });
        }
        private static void CollectNodes(ProductionNode node, List<ProductionNode> result)
        { result.Add(node); foreach (var child in node.Children) CollectNodes(child, result); }

        private Dropdown CreateDropdown(Transform parent, float x, float width)
        {
            var rect = Box(parent, "Auswahl", Color.white); Place(rect, x, 18, width, 28);
            var dropdown = rect.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
            var caption = Label(rect, "", 12); Stretch(caption.rectTransform, 4); caption.rectTransform.offsetMax = new Vector2(-22, -4);
            dropdown.captionText = caption;
            var arrow = Label(rect, "▾", 14); Place(arrow.rectTransform, width - 20, 1, 16, 26);
            var template = Box(rect, "Template", Border);
            template.anchorMin = Vector2.zero; template.anchorMax = new Vector2(1, 0); template.pivot = new Vector2(.5f, 1);
            template.anchoredPosition = new Vector2(0, -2); template.sizeDelta = new Vector2(0, 180);
            var viewport = Box(template, "Viewport", Color.white); Stretch(viewport, 2); viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 32);
            var item = Box(contentRect, "Item", Color.white); Stretch(item, 0);
            var toggle = item.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var highlight = Box(item, "Ausgewählt", Border); Stretch(highlight, 0); toggle.graphic = highlight.GetComponent<Image>();
            highlight.GetComponent<Image>().raycastTarget = false;
            var label = Label(item, "", 12); Stretch(label.rectTransform, 4);
            dropdown.itemText = label;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = contentRect;
            scroll.scrollSensitivity = 64;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = template; template.gameObject.SetActive(false);
            return dropdown;
        }

        private static string Format(Ingredient ingredient) => ingredient.Amount.ToString("0.###", CultureInfo.CurrentCulture) + " × " + RecipeCatalog.Name(ingredient.Item);

        private RectTransform Box(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().sprite = roundedSprite; go.GetComponent<Image>().type = Image.Type.Sliced;
            return (RectTransform)go.transform;
        }
        private Text Label(Transform parent, string value, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.text = value;
            text.color = Ink; text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false; text.raycastTarget = false;
            return text;
        }
        private RectTransform Button(Transform parent, string value, Action action)
        {
            var rect = Box(parent, "Button", RowColor); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            var text = Label(rect, value, 12); Stretch(text.rectTransform, 0);
            text.rectTransform.offsetMin = new Vector2(8, 2);
            text.rectTransform.offsetMax = new Vector2(-8, -2);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return rect;
        }
        private void AddIcon(RectTransform parent, CItem item, float size = 20)
        {
            var sprite = item.icon?.Asset; if (sprite == null) return;
            var icon = Box(parent, "Icon", Color.white); icon.GetComponent<Image>().sprite = sprite;
            icon.GetComponent<Image>().type = Image.Type.Simple;
            icon.GetComponent<Image>().preserveAspect = true; icon.GetComponent<Image>().raycastTarget = false;
            icon.anchorMin = icon.anchorMax = new Vector2(0, .5f);
            icon.pivot = new Vector2(0, .5f);
            icon.anchoredPosition = new Vector2(4, 0);
            icon.sizeDelta = new Vector2(size, size);
            var text = parent.GetComponentInChildren<Text>(); text.rectTransform.offsetMin = new Vector2(size + 10, 2);
        }
        private RectTransform Scroll(Transform parent, string name, float x, float y, float width, float height, bool showScrollbar = false)
        {
            var container = Box(parent, name, Background); Place(container, x, y, width, height);
            var viewport = Box(container, "Viewport", Background);
            Stretch(viewport, 0);
            if (showScrollbar) viewport.offsetMax = new Vector2(-20, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false);
            var rect = (RectTransform)content.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 2; layout.padding = new RectOffset(4, 4, 4, 4);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = container.gameObject.AddComponent<ScrollRect>(); scroll.content = rect; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            if (showScrollbar)
            {
                var track = Box(container, "Scrollbar", new Color(.84f, .83f, .95f));
                track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
                track.offsetMin = new Vector2(-18, 4); track.offsetMax = new Vector2(-3, -4);
                var slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
                slidingArea.transform.SetParent(track, false);
                Stretch((RectTransform)slidingArea.transform, 2);
                var handle = Box(slidingArea.transform, "Handle", Border);
                Stretch(handle, 0);
                var scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
                scrollbar.value = 1;
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            return rect;
        }
        private Text Line(Transform parent, string value, int size = 12, int height = 22)
        {
            var text = Label(parent, value, size);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            // Text supplies preferred height once the scroll layout assigns its width.
            return text;
        }
        private static void Height(RectTransform rect, float height)
        { var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.minHeight = height; layout.preferredHeight = height; }
        private static void Stretch(RectTransform rect, float padding)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(padding, padding); rect.offsetMax = new Vector2(-padding, -padding); }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
        private static void Clear(Transform parent)
        { for (int i = parent.childCount - 1; i >= 0; i--) { var child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); } }
        private void DisposeUI()
        {
            if (launcherObject != null) Destroy(launcherObject);
            launcherObject = null;
            repeatBuildButton = null;
            activeBlueprint = null;
            if (canvasObject != null) Destroy(canvasObject);
            canvasObject = null; panel = null; selected = null; choices.Clear(); collapsed.Clear();
            if (roundedSprite != null) Destroy(roundedSprite);
            if (roundedTexture != null) Destroy(roundedTexture);
        }
        private void CreateRoundedSprite()
        {
            const int size = 32, radius = 8;
            roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + .5f, radius, size - radius);
                var cy = Mathf.Clamp(y + .5f, radius, size - radius);
                var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f));
            }
            roundedTexture.SetPixels(pixels); roundedTexture.Apply();
            roundedSprite = Sprite.Create(roundedTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
        private void OnDestroy()
        { harmony?.UnpatchSelf(); DisposeUI(); PictogramImage.ReleaseAssets(); if (instance == this) instance = null; }
    }
}
