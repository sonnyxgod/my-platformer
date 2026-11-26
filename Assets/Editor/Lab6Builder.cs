using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Інструмент редактора для лабораторної 6. Меню Tools → Lab 6:
//   1) шар Pickup, імпорт спрайтів інтерфейсу, шрифт TMP, канали подій, префаби CoinPickupFX і Coin;
//   2) префаб GameplayUI: Canvas із HUD і меню паузи, EventSystem, LEVEL_BOOTSTRAP, PAUSE;
//   3) Level01: HUD, монети, канали здоров'я гравця, вихід з рівня;
//   4) Level02: копія Level01 з іншою геометрією, ворогами й монетами;
//   5) MainMenu і список сцен збірки.
// Кожен пункт можна запускати повторно.
public static class Lab6Builder
{
    // ---------- шляхи ----------
    private const string UiArt = "Assets/Art/UI";
    private const string FontTtf = "Assets/Art/UI/Fonts/PressStart2P.ttf";
    private const string FontAssetPath = "Assets/Art/UI/Fonts/PressStart2P SDF.asset";
    private const string SoulSprite = "Assets/Art/Pickups/soul.png";
    private const string EventsFolder = "Assets/Data/Events";
    private const string FxPrefab = "Assets/Prefabs/CoinPickupFX.prefab";
    private const string CoinPrefab = "Assets/Prefabs/Coin.prefab";
    private const string UiPrefab = "Assets/Prefabs/GameplayUI.prefab";
    private const string EnemyPrefab = "Assets/Prefabs/Enemy.prefab";
    private const string Level01 = "Assets/Scenes/Level01.unity";
    private const string Level02 = "Assets/Scenes/Level02.unity";
    private const string MainMenu = "Assets/Scenes/MainMenu.unity";
    private const string SceneTemplate = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    private const string RuleTilePath = "Assets/Art/Tilesets/GroundRuleTile.asset";
    private const string TilesFolder = "Assets/Art/Tilesets/Tiles";

    // ---------- палітра лабораторної 1 ----------
    private static readonly Color Bone = Hex("E6EDE4");     // інтерфейс
    private static readonly Color Accent = Hex("7FF5E6");   // акцент (очі, душі)
    private static readonly Color Hero = Hex("9B2D3C");     // герой — колір здоров'я
    private static readonly Color Dark = Hex("1A1720");

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    // ================================================================ 1
    [MenuItem("Tools/Lab 6/1. Setup Assets")]
    public static void SetupAssets()
    {
        int pickup = EnsureLayer("Pickup");
        int player = LayerMask.NameToLayer("Player");
        // Підбирання — справа лише монети й гравця
        for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(pickup, i, i != player);

        ImportSprite(SoulSprite, Vector4.zero);
        ImportSprite($"{UiArt}/ui_frame.png", new Vector4(4, 4, 4, 4));
        ImportSprite($"{UiArt}/ui_button.png", new Vector4(5, 5, 5, 5));
        ImportSprite($"{UiArt}/ui_fill.png", Vector4.zero);

        CreateFontAsset();

        EnsureFolder(EventsFolder);
        var hp = EventAsset("PlayerHPChanged");
        EventAsset("PlayerMaxHPChanged");
        var coins = EventAsset("CoinsChanged");

        // --- ефект підбирання ---
        var fxGo = new GameObject("CoinPickupFX");
        var ps = fxGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 3f;
        main.startSize = 0.12f;
        main.gravityModifier = 0.5f;
        main.startColor = Accent;
        main.stopAction = ParticleSystemStopAction.Destroy; // інакше клони накопичуються
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.15f;
        var pr = fxGo.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat")
                            ?? AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        pr.sortingLayerName = "Foreground";
        pr.sortingOrder = 10;
        var fx = PrefabUtility.SaveAsPrefabAsset(fxGo, FxPrefab).GetComponent<ParticleSystem>();
        Object.DestroyImmediate(fxGo);

        // --- монета (душа) ---
        var coinGo = new GameObject("Coin") { layer = pickup };
        var sr = coinGo.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoulSprite);
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -2;
        var col = coinGo.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.4f;   // щедре підбирання: трохи більше за спрайт
        var coin = coinGo.AddComponent<Coin>();
        var so = new SerializedObject(coin);
        so.FindProperty("coinsChanged").objectReferenceValue = coins;
        so.FindProperty("pickupEffect").objectReferenceValue = fx;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(coinGo, CoinPrefab);
        Object.DestroyImmediate(coinGo);

        AssetDatabase.SaveAssets();
        Debug.Log($"Lab 6/1: шар Pickup ({pickup}), спрайти, шрифт, канали {hp.name}…, префаби CoinPickupFX і Coin готові.");
    }

    private static void CreateFontAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return;
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
        if (font == null) { Debug.LogError("Не знайдено " + FontTtf); return; }

        var fa = TMP_FontAsset.CreateFontAsset(font, 32, 4, GlyphRenderMode.SDFAA, 1024, 1024,
                                               AtlasPopulationMode.Dynamic, true);
        fa.name = "PressStart2P SDF";
        AssetDatabase.CreateAsset(fa, FontAssetPath);
        fa.atlasTextures[0].name = "PressStart2P Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = "PressStart2P Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters("0123456789 АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя'’.,:!?-—«»");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
    }

    private static IntEventSO EventAsset(string name)
    {
        string path = $"{EventsFolder}/{name}.asset";
        var e = AssetDatabase.LoadAssetAtPath<IntEventSO>(path);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<IntEventSO>();
            AssetDatabase.CreateAsset(e, path);
        }
        return e;
    }

    // ================================================================ 2
    [MenuItem("Tools/Lab 6/2. Build GameplayUI Prefab")]
    public static void BuildGameplayUI()
    {
        var root = new GameObject("GameplayUI");
        var canvas = MakeCanvas(root.transform);

        // ---------- HUD ----------
        var hud = UiRect("HUD", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Смуга здоров'я: верхній лівий кут, Pivot X = 0
        var hpBar = UiRect("HPBar", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(420, 48));
        var hpBg = hpBar.gameObject.AddComponent<Image>();
        Sliced(hpBg, "ui_frame", 2f);
        var fillRt = UiRect("Fill", hpBar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fillRt.offsetMin = new Vector2(13, 13);
        fillRt.offsetMax = new Vector2(-13, -13);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = UiSprite("ui_fill");
        fill.color = Hero;
        fill.type = Image.Type.Filled;          // без цього fillAmount не працює
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;
        var hb = hpBar.gameObject.AddComponent<HealthBar>();
        var hso = new SerializedObject(hb);
        hso.FindProperty("healthChanged").objectReferenceValue = LoadEvent("PlayerHPChanged");
        hso.FindProperty("maxHealthChanged").objectReferenceValue = LoadEvent("PlayerMaxHPChanged");
        hso.FindProperty("fill").objectReferenceValue = fill;
        hso.ApplyModifiedPropertiesWithoutUndo();

        // Лічильник душ: верхній правий кут, Pivot X = 1 — число росте вліво
        var group = UiRect("CoinGroup", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(200, 52));
        var hlg = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fit = group.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var icon = UiRect("CoinIcon", group, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 49));
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoulSprite);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        var le = icon.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 42; le.preferredHeight = 49;

        var label = Label("CoinLabel", group, "0", 36, Bone, TextAlignmentOptions.MidlineRight);
        label.rectTransform.pivot = new Vector2(1, 1);
        var cc = label.gameObject.AddComponent<CoinCounter>();
        var cso = new SerializedObject(cc);
        cso.FindProperty("coinsChanged").objectReferenceValue = LoadEvent("CoinsChanged");
        cso.FindProperty("label").objectReferenceValue = label;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // ---------- меню паузи: ПОРУЧ із HUD, а не всередині ----------
        var popup = UiRect("PausePopup", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        // Overlay — ПЕРШИЙ дочірній: у uGUI порядок в ієрархії — це порядок малювання
        var overlay = UiRect("Overlay", popup, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var ovImg = overlay.gameObject.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 180f / 255f);
        ovImg.raycastTarget = true;             // саме він ловить кліки, що летять у сцену позаду
        var panel = UiRect("Panel", popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 600));
        Sliced(panel.gameObject.AddComponent<Image>(), "ui_frame", 1f);
        var title = Label("Title", panel, "ПАУЗА", 48, Accent, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0, 200), new Vector2(640, 80));

        var pauseGo = new GameObject("PAUSE");
        pauseGo.transform.SetParent(root.transform, false);
        var pc = pauseGo.AddComponent<PauseController>();

        var bResume = MakeButton(panel, "BtnResume", "Продовжити", new Vector2(0, 60));
        var bRestart = MakeButton(panel, "BtnRestart", "Рестарт", new Vector2(0, -60));
        var bMenu = MakeButton(panel, "BtnMenu", "У меню", new Vector2(0, -180));
        UnityEventTools.AddPersistentListener(bResume.onClick, pc.Resume);
        UnityEventTools.AddPersistentListener(bRestart.onClick, pc.Restart);
        UnityEventTools.AddPersistentListener(bMenu.onClick, pc.ToMenu);

        var pso = new SerializedObject(pc);
        pso.FindProperty("pausePopup").objectReferenceValue = popup.gameObject;
        pso.FindProperty("firstButton").objectReferenceValue = bResume.gameObject;
        pso.ApplyModifiedPropertiesWithoutUndo();
        popup.gameObject.SetActive(false);      // за замовчуванням меню вимкнене

        MakeEventSystem(root.transform);

        var boot = new GameObject("LEVEL_BOOTSTRAP");
        boot.transform.SetParent(root.transform, false);
        var lb = boot.AddComponent<LevelBootstrap>();
        var bso = new SerializedObject(lb);
        bso.FindProperty("coinsChanged").objectReferenceValue = LoadEvent("CoinsChanged");
        bso.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Lab 6/2: префаб GameplayUI зібрано.");
    }

    // ================================================================ 3
    [MenuItem("Tools/Lab 6/3. Setup Level01")]
    public static void SetupLevel01()
    {
        var scene = OpenScene(Level01);
        PlaceUiAndPlayer(scene);

        PlaceCoins(scene, new[]
        {
            new Vector2(8.5f, 3.6f), new Vector2(12.5f, 5.6f), new Vector2(16f, 3.6f), new Vector2(19.5f, 6.6f),
            new Vector2(24f, 3.6f), new Vector2(30f, 3.6f), new Vector2(36.5f, 5.6f), new Vector2(42.5f, 6.6f),
            new Vector2(49f, 3.6f), new Vector2(53.5f, 3.6f), new Vector2(64f, 5.2f), new Vector2(72f, 3.6f),
            new Vector2(76f, 3.6f), new Vector2(84f, 3.6f), new Vector2(90f, 3.6f), new Vector2(98.5f, 5.6f),
            new Vector2(106f, 3.6f), new Vector2(114f, 3.6f),
        });
        SetupExit(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Lab 6/3: Level01 — HUD, 18 душ, вихід на статуї. Сцену збережено.");
    }

    // ================================================================ 4
    [MenuItem("Tools/Lab 6/4. Create Level02")]
    public static void CreateLevel02()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.DeleteAsset(Level02);
        if (!AssetDatabase.CopyAsset(Level01, Level02)) { Debug.LogError("Не вдалося скопіювати Level01."); return; }
        var scene = EditorSceneManager.OpenScene(Level02, OpenSceneMode.Single);

        const int W = 90, H = 12;
        PaintLevel02(W, H);

        // Важіль і його платформа — механіка першого рівня, тут її немає
        foreach (var n in new[] { "Lever", "LeverPlatform" })
        {
            var go = Find(scene, n);
            if (go != null) Object.DestroyImmediate(go);
        }

        // Вороги іншого складу: два стрільці на висотах і два рашери на рівнинах
        var enemies = Find(scene, "Enemies").transform;
        for (int i = enemies.childCount - 1; i >= 0; i--) Object.DestroyImmediate(enemies.GetChild(i).gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
        SpawnEnemy(prefab, enemies, "Shooter", new Vector3(35f, 5f), "Enemy_Shooter");
        SpawnEnemy(prefab, enemies, "Rusher", new Vector3(48f, 3f), "Enemy_Rusher");
        SpawnEnemy(prefab, enemies, "Shooter", new Vector3(74f, 6f), "Enemy_Shooter_2");
        SpawnEnemy(prefab, enemies, "Rusher", new Vector3(82f, 3f), "Enemy_Rusher_2");

        Find(scene, "Checkpoint_1").transform.position = new Vector3(25.5f, 3f);
        Find(scene, "Checkpoint_2").transform.position = new Vector3(65.5f, 3f);
        Find(scene, "Finish").transform.position = new Vector3(W - 2.5f, 3f);

        var bounds = Find(scene, "LevelBounds").GetComponent<BoxCollider2D>();
        bounds.size = new Vector2(W, 14);
        bounds.offset = new Vector2(W / 2f, 7);

        // Паралакс: ширина ≥ ширина екрана + (1 − f) × шлях камери (≈ 70)
        SetBg(Find(scene, "BG_Far"), 39f, 80f);
        SetBg(Find(scene, "BG_Near"), 28f, 58f);

        PlaceCoins(scene, new[]
        {
            new Vector2(6.5f, 3.6f), new Vector2(12.5f, 5.6f), new Vector2(14.5f, 6.6f), new Vector2(20.5f, 5.5f),
            new Vector2(28f, 3.6f), new Vector2(35.5f, 5.6f), new Vector2(44f, 3.6f), new Vector2(52f, 3.6f),
            new Vector2(57.5f, 5.2f), new Vector2(62f, 5.2f), new Vector2(73.5f, 6.6f), new Vector2(86f, 3.6f),
        });

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Lab 6/4: Level02 створено (90 клітинок, 4 вороги, 12 душ). Сцену збережено.");
    }

    private static void PaintLevel02(int W, int H)
    {
        var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
        var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        Tilemap Map(string n) => maps.First(m => m.name == n);
        Tilemap ground = Map("Ground"), decor = Map("Decor"), hazards = Map("Hazards"), back = Map("Background");
        foreach (var m in new[] { ground, decor, hazards, back }) m.ClearAllTiles();

        var g = new bool[W, H];
        void Fill(int x0, int x1, int y0, int y1, bool v = true)
        {
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) g[x, y] = v;
        }

        Fill(0, W - 1, 0, 2);                                   // земля, поверхня на висоті 3
        Fill(0, 0, 3, 9); Fill(W - 1, W - 1, 3, 9);             // стіни
        Fill(10, 11, 3, 3); Fill(12, 13, 3, 4); Fill(14, 15, 3, 5);   // сходи вгору
        Fill(19, 22, 1, 2, false);                              // яма з шипами
        Fill(30, 40, 3, 4);                                     // плато стрільця
        Fill(56, 58, 1, 2, false); Fill(61, 63, 1, 2, false);   // дві ями поспіль
        Fill(70, 77, 3, 5);                                     // високий уступ стрільця

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (g[x, y]) ground.SetTile(new Vector3Int(x, y, 0), rule);

        var spikes = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/hazard_spikes.asset");
        foreach (var (a, b) in new[] { (19, 22), (56, 58), (61, 63) })
            for (int x = a; x <= b; x++) hazards.SetTile(new Vector3Int(x, 1, 0), spikes);

        var decorMap = new Dictionary<int, int> { { 4, 8 }, { 8, 26 }, { 25, 11 }, { 33, 27 }, { 44, 9 }, { 50, 35 },
                                                  { 53, 26 }, { 66, 27 }, { 73, 8 }, { 80, 11 }, { 85, 26 } };
        foreach (var kv in decorMap)
            for (int y = 3; y < H; y++)
                if (g[kv.Key, y - 1] && !g[kv.Key, y])
                {
                    decor.SetTile(new Vector3Int(kv.Key, y, 0), LoadTile("tileset_" + kv.Value));
                    break;
                }

        foreach (int x0 in new[] { 3, 42, 79 })
            for (int k = 0; k < 7; k++)
            {
                back.SetTile(new Vector3Int(x0 + k, 3, 0), LoadTile("tileset_" + (64 + k)));
                back.SetTile(new Vector3Int(x0 + k, 4, 0), LoadTile("tileset_" + (51 + k)));
            }
    }

    // ================================================================ 5
    [MenuItem("Tools/Lab 6/5. Create MainMenu and Scene List")]
    public static void CreateMainMenu()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.DeleteAsset(MainMenu);
        AssetDatabase.CopyAsset(SceneTemplate, MainMenu);
        var scene = EditorSceneManager.OpenScene(MainMenu, OpenSceneMode.Single);

        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 5.625f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = Dark;

        // Фон — ті самі спрайти GothicVania, що й у паралаксі рівня
        BgSprite(scene, "Sky", "Assets/Art/Sprites/background.png", new Vector3(0, 0.6f), 0);
        BgSprite(scene, "Graveyard", "Assets/Art/Sprites/graveyard.png", new Vector3(0, -1.9f), 1);
        LightAllLayers(scene);

        var root = new GameObject("MenuUI");
        var canvas = MakeCanvas(root.transform);
        var shade = UiRect("Shade", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var shadeImg = shade.gameObject.AddComponent<Image>();
        shadeImg.color = new Color(Dark.r, Dark.g, Dark.b, 0.45f);

        var title = Label("Title", canvas, "ДУШІ ЦВИНТАРЯ", 72, Bone, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        Place(title.rectTransform, new Vector2(0, -230), new Vector2(1600, 120));
        var tag = Label("Tagline", canvas, "збери душі - дійди до статуї", 24, Accent, TextAlignmentOptions.Center);
        tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        Place(tag.rectTransform, new Vector2(0, -330), new Vector2(1400, 50));

        var panel = UiRect("Buttons", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(680, 440));
        Sliced(panel.gameObject.AddComponent<Image>(), "ui_frame", 1f);

        var menuGo = new GameObject("Menu");
        var menu = menuGo.AddComponent<MainMenuUI>();
        var bNew = MakeButton(panel, "BtnNewGame", "Нова гра", new Vector2(0, 110));
        var bCont = MakeButton(panel, "BtnContinue", "Продовжити", new Vector2(0, -5));
        var bQuit = MakeButton(panel, "BtnQuit", "Вийти", new Vector2(0, -120));
        UnityEventTools.AddPersistentListener(bNew.onClick, menu.NewGame);
        UnityEventTools.AddPersistentListener(bCont.onClick, menu.Continue);
        UnityEventTools.AddPersistentListener(bQuit.onClick, menu.Quit);
        var mso = new SerializedObject(menu);
        mso.FindProperty("continueButton").objectReferenceValue = bCont;
        mso.ApplyModifiedPropertiesWithoutUndo();

        var es = MakeEventSystem(null);
        es.GetComponent<EventSystem>().firstSelectedGameObject = bNew.gameObject;

        EditorSceneManager.SaveScene(scene);

        // 0 — меню, 1 — перший рівень, 2 — наступний = поточний + 1
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenu, true),
            new EditorBuildSettingsScene(Level01, true),
            new EditorBuildSettingsScene(Level02, true),
        };
        Debug.Log("Lab 6/5: MainMenu створено; Scene List: 0 MainMenu, 1 Level01, 2 Level02.");
    }

    // ================================================================ сервіс
    [MenuItem("Tools/Lab 6/Delete Save File")]
    public static void DeleteSave()
    {
        SaveSystem.Delete();
        GameProgress.Reset();
        Debug.Log("Lab 6: save.json видалено — «Продовжити» в меню знову неактивна.");
    }

    // ================================================================ спільне
    private static void PlaceUiAndPlayer(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "GameplayUI") Object.DestroyImmediate(go);
        var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefab), scene);
        ui.name = "GameplayUI";

        var player = Find(scene, "Player");
        var ph = player.GetComponent<PlayerHealth>();
        var so = new SerializedObject(ph);
        so.FindProperty("healthChanged").objectReferenceValue = LoadEvent("PlayerHPChanged");
        so.FindProperty("maxHealthChanged").objectReferenceValue = LoadEvent("PlayerMaxHPChanged");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlaceCoins(Scene scene, Vector2[] points)
    {
        var gameplay = Find(scene, "GAMEPLAY");
        var old = Find(scene, "Coins");
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject("Coins").transform;
        group.SetParent(gameplay != null ? gameplay.transform : null, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
        for (int i = 0; i < points.Length; i++)
        {
            var c = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            c.name = $"Coin_{i + 1:00}";
            c.transform.position = points[i];
        }
    }

    private static void SetupExit(Scene scene)
    {
        var finish = Find(scene, "Finish");
        var box = finish.GetComponent<BoxCollider2D>();
        if (box == null) box = finish.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(2f, 4f);
        box.offset = new Vector2(0f, 2f);
        if (finish.GetComponent<LevelExit>() == null) finish.AddComponent<LevelExit>();
    }

    private static void SpawnEnemy(GameObject prefab, Transform parent, string type, Vector3 pos, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        var so = new SerializedObject(go.GetComponent<EnemyController>());
        so.FindProperty("stats").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<EnemyStatsSO>($"Assets/Data/Enemies/Enemy_{type}.asset");
        so.ApplyModifiedProperties();
    }

    private static void SetBg(GameObject bg, float x, float width)
    {
        var p = bg.transform.position;
        bg.transform.position = new Vector3(x, p.y, p.z);
        var sr = bg.GetComponent<SpriteRenderer>();
        sr.size = new Vector2(width, sr.size.y);
    }

    private static void BgSprite(Scene scene, string name, string path, Vector3 pos, int order)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        sr.sortingLayerName = "Background";
        sr.sortingOrder = order;
    }

    // Глобальне світло шаблонної сцени створене до появи шарів Background і т.д. —
    // вмикаємо його для всіх Sorting Layers, інакше фон меню чорний
    private static void LightAllLayers(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "Light2D") continue;
                var so = new SerializedObject(c);
                var arr = so.FindProperty("m_ApplyToSortingLayers");
                if (arr == null) continue;
                var layers = SortingLayer.layers;
                arr.arraySize = layers.Length;
                for (int i = 0; i < layers.Length; i++) arr.GetArrayElementAtIndex(i).intValue = layers[i].id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
    }

    private static RectTransform MakeCanvas(Transform parent)
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }

    private static GameObject MakeEventSystem(Transform parent)
    {
        var go = new GameObject("EventSystem", typeof(EventSystem));
        if (parent != null) go.transform.SetParent(parent, false);
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        return go;
    }

    private static RectTransform UiRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var rt = UiRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static Button MakeButton(Transform parent, string name, string caption, Vector2 pos)
    {
        var rt = UiRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(520, 90));
        var img = rt.gameObject.AddComponent<Image>();
        Sliced(img, "ui_button", 1.5f);
        var b = rt.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.75f, 1f, 0.96f);   // відтінок акценту
        cb.selectedColor = new Color(0.75f, 1f, 0.96f);
        cb.pressedColor = new Color(0.55f, 0.55f, 0.6f);
        cb.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);
        b.colors = cb;
        var t = Label("Text", rt, caption, 28, Bone, TextAlignmentOptions.Center);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;
        return b;
    }

    private static void Sliced(Image img, string sprite, float ppuMultiplier)
    {
        img.sprite = UiSprite(sprite);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = ppuMultiplier;
    }

    private static Sprite UiSprite(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/{n}.png");
    private static IntEventSO LoadEvent(string n) => AssetDatabase.LoadAssetAtPath<IntEventSO>($"{EventsFolder}/{n}.asset");
    private static Tile LoadTile(string n) => AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/{n}.asset");

    private static Scene OpenScene(string path)
    {
        var active = SceneManager.GetActiveScene();
        if (active.path == path) return active;
        EditorSceneManager.SaveOpenScenes();
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static void ImportSprite(string path, Vector4 border)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogError("Не знайдено " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 16;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;   // для 9-slice
        st.spriteAlignment = (int)SpriteAlignment.Center;
        ti.SetTextureSettings(st);
        ti.spriteBorder = border;
        ti.SaveAndReimport();
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = name;
                tm.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        Debug.LogError("Немає вільного шару для " + name);
        return 0;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
