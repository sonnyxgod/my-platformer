using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Інструмент редактора для лабораторної 4.
// Меню Tools → Lab 4:
//   1) створює GroundRuleTile (11 правил) і тайли декору/небезпеки/фону з нарізаного тайлсету;
//   2) малює рівень 120 × 12 клітинок: три біти навчання (x 0–79, лабораторна 4)
//      і бойову арену для ворогів (x 80–119, лабораторна 5).
// Після цього рівень можна доправляти вручну пензлями Tile Palette.
public static class Lab4LevelBuilder
{
    private const string TilesetPath = "Assets/Art/Tilesets/tileset.png";
    private const string SpikesPath = "Assets/Art/Tilesets/hazard_spikes.png";
    private const string TilesFolder = "Assets/Art/Tilesets/Tiles";
    private const string RuleTilePath = "Assets/Art/Tilesets/GroundRuleTile.asset";

    private const int W = 120, H = 12;

    // Позначки сусідів у Rule Tile
    private const int Any = 0;                                            // байдуже
    private const int This = RuleTile.TilingRuleOutput.Neighbor.This;     // стрілка
    private const int Not = RuleTile.TilingRuleOutput.Neighbor.NotThis;   // хрестик

    [MenuItem("Tools/Lab 4/1. Create Rule Tile and Tiles")]
    public static void CreateTiles()
    {
        var sprites = LoadSprites();
        if (sprites == null) return;

        EnsureFolder(TilesFolder);

        var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
        if (rule == null)
        {
            rule = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(rule, RuleTilePath);
        }

        Sprite S(int i) => sprites["tileset_" + i];

        rule.m_DefaultSprite = S(59);
        rule.m_DefaultColliderType = Tile.ColliderType.Grid;
        rule.m_TilingRules = new List<RuleTile.TilingRule>();

        // Порядок сусідів: верх-ліво, верх, верх-право, ліво, право, низ-ліво, низ, низ-право.
        // Unity перевіряє правила ЗГОРИ ВНИЗ і бере перше, що збіглося.
        AddRule(rule, new[] { Any, Not, Any, Not, This, Any, This, Any }, S(20));                 // зовнішній кут: верх-ліво
        AddRule(rule, new[] { Any, Not, Any, This, Not, Any, This, Any }, S(21));                 // зовнішній кут: верх-право
        AddRule(rule, new[] { Any, Not, Any, This, This, Any, Any, Any }, S(22), S(23), S(25));   // край: верх (трава, випадковий варіант)
        AddRule(rule, new[] { Any, Not, Any, Any, Any, Any, Any, Any }, S(22));                   // верх тонкої ділянки (стовп, сходинка)
        AddRule(rule, new[] { Any, This, Any, Not, This, Any, Not, Any }, S(58));                 // зовнішній кут: низ-ліво
        AddRule(rule, new[] { Any, This, Any, This, Not, Any, Not, Any }, S(59));                 // зовнішній кут: низ-право
        AddRule(rule, new[] { Any, This, Any, Any, Any, Any, Not, Any }, S(60));                  // край: низ
        AddRule(rule, new[] { Any, This, Any, Not, Any, Any, Any, Any }, S(37));                  // край: ліво
        AddRule(rule, new[] { Any, This, Any, Any, Not, Any, Any, Any }, S(38));                  // край: право
        AddRule(rule, new[] { Any, This, Any, This, This, Any, This, Any }, S(59), S(60));        // середина
        AddRule(rule, new[] { Any, Any, Any, Any, Any, Any, Any, Any }, S(59));                   // без умов — ОСТАННІМ

        EditorUtility.SetDirty(rule);

        // Звичайні тайли: декор, задній план, небезпека
        foreach (int i in new[] { 8, 9, 11, 26, 27, 35 }) GetTile("tileset_" + i, S(i), Tile.ColliderType.None);
        for (int i = 51; i <= 57; i++) GetTile("tileset_" + i, S(i), Tile.ColliderType.None);
        for (int i = 64; i <= 70; i++) GetTile("tileset_" + i, S(i), Tile.ColliderType.None);

        var spikes = AssetDatabase.LoadAssetAtPath<Sprite>(SpikesPath);
        if (spikes == null) Debug.LogWarning($"Не знайдено {SpikesPath} — імпортуйте hazard_spikes.png як Sprite (Single), PPU 16.");
        else GetTile("hazard_spikes", spikes, Tile.ColliderType.Grid);

        AssetDatabase.SaveAssets();
        Debug.Log($"Lab 4: створено {RuleTilePath} ({rule.m_TilingRules.Count} правил) і тайли в {TilesFolder}.");
    }

    [MenuItem("Tools/Lab 4/2. Paint Level")]
    public static void PaintLevel()
    {
        var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
        if (rule == null) { Debug.LogError("Спершу виконайте Tools → Lab 4 → 1. Create Rule Tile and Tiles."); return; }

        var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        Tilemap Map(string n) => maps.FirstOrDefault(m => m.name == n);
        Tilemap ground = Map("Ground"), decor = Map("Decor"), hazards = Map("Hazards"), back = Map("Background");
        if (ground == null || decor == null || hazards == null || back == null)
        {
            Debug.LogError("Не знайдено всі чотири Tilemap: Ground, Decor, Hazards, Background (крок 1).");
            return;
        }

        Undo.RegisterCompleteObjectUndo(new Object[] { ground, decor, hazards, back }, "Paint Lab 4 level");
        foreach (var m in new[] { ground, decor, hazards, back }) m.ClearAllTiles();

        // ---------- Геометрія за картою ----------
        var g = new bool[W, H];
        void Fill(int x0, int x1, int y0, int y1, bool v = true)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++) g[x, y] = v;
        }

        Fill(0, W - 1, 0, 2);                      // земля, поверхня на висоті 3
        Fill(0, 0, 3, 9); Fill(W - 1, W - 1, 3, 9);  // стіни по краях
        Fill(12, 13, 3, 4); Fill(19, 20, 3, 5);    // Біт 1: перешкоди на рівній землі
        Fill(33, 34, 3, 4); Fill(35, 38, 1, 2, false);   // Біт 2: перешкода + яма з шипами
        Fill(42, 43, 3, 5); Fill(44, 47, 1, 2, false);
        Fill(58, 69, 0, 2, false);                 // Біт 3: прірва 12 клітинок
        Fill(95, 102, 3, 4);                       // Арена (Л5): уступ для стрільця

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (g[x, y]) ground.SetTile(new Vector3Int(x, y, 0), rule);

        // ---------- Небезпека ----------
        var spikes = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/hazard_spikes.asset");
        if (spikes != null)
        {
            for (int x = 35; x <= 38; x++) hazards.SetTile(new Vector3Int(x, 1, 0), spikes);
            for (int x = 44; x <= 47; x++) hazards.SetTile(new Vector3Int(x, 1, 0), spikes);
            for (int x = 58; x <= 69; x++) hazards.SetTile(new Vector3Int(x, 0, 0), spikes);
        }

        // ---------- Декор (без колайдера, «тихіший» за геометрію) ----------
        var decorMap = new Dictionary<int, int> { { 4, 8 }, { 6, 26 }, { 9, 11 }, { 16, 27 }, { 23, 9 }, { 28, 35 },
                                                  { 31, 26 }, { 40, 27 }, { 50, 8 }, { 54, 11 }, { 72, 26 }, { 75, 27 },
                                                  { 84, 9 }, { 90, 26 }, { 106, 27 }, { 113, 8 } };
        foreach (var kv in decorMap)
            if (g[kv.Key, 2] && !g[kv.Key, 3])
                decor.SetTile(new Vector3Int(kv.Key, 3, 0), Load("tileset_" + kv.Value));

        // ---------- Тайловий задній план: пагорби за землею ----------
        foreach (int x0 in new[] { 3, 22, 48, 71, 84, 108 })
            for (int k = 0; k < 7; k++)
            {
                back.SetTile(new Vector3Int(x0 + k, 3, 0), Load("tileset_" + (64 + k)));
                back.SetTile(new Vector3Int(x0 + k, 4, 0), Load("tileset_" + (51 + k)));
            }

        // Декор і задній план приглушуємо кольором Tilemap — нижчий контраст, ніж у геометрії
        decor.color = new Color(0.75f, 0.75f, 0.8f, 1f);
        back.color = new Color(0.5f, 0.5f, 0.6f, 1f);

        foreach (var m in new[] { ground, decor, hazards, back }) EditorUtility.SetDirty(m);
        EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);
        Debug.Log($"Lab 4/5: рівень {W} × {H} намальовано. Збережіть сцену (Ctrl+S).");
    }

    // ---------------- допоміжне ----------------
    private static Dictionary<string, Sprite> LoadSprites()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TilesetPath).OfType<Sprite>()
            .GroupBy(s => s.name).ToDictionary(gr => gr.Key, gr => gr.First());
        if (sprites.Count == 0)
        {
            Debug.LogError($"Не знайдено нарізаних спрайтів у {TilesetPath} (лабораторна 2).");
            return null;
        }
        return sprites;
    }

    private static void AddRule(RuleTile rule, int[] neighbors, params Sprite[] sprites)
    {
        var r = new RuleTile.TilingRule
        {
            m_Id = rule.m_TilingRules.Count,
            m_NeighborPositions = new List<Vector3Int>
            {
                new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
                new Vector3Int(-1, 0, 0),                          new Vector3Int(1, 0, 0),
                new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
            },
            m_Neighbors = new List<int>(neighbors),
            m_Sprites = sprites,
            m_Output = sprites.Length > 1
                ? RuleTile.TilingRuleOutput.OutputSprite.Random
                : RuleTile.TilingRuleOutput.OutputSprite.Single,
            m_ColliderType = Tile.ColliderType.Grid,
        };
        rule.m_TilingRules.Add(r);
    }

    private static Tile GetTile(string name, Sprite sprite, Tile.ColliderType collider)
    {
        string path = $"{TilesFolder}/{name}.asset";
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(t, path);
        }
        t.sprite = sprite;
        t.colliderType = collider;
        EditorUtility.SetDirty(t);
        return t;
    }

    private static Tile Load(string name) => AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/{name}.asset");

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
