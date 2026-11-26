using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Інструмент редактора для лабораторної 5.
// Меню Tools → Lab 5:
//   1) створює анімації ворога, Animator Controller, префаби Projectile і Enemy;
//   2) розставляє чотири типи ворогів на рівні й налаштовує гравцю здоров'я та стрибок-удар;
//   3) додає гравцю удар мечем (PlayerAttack, дуга змаху, звук).
// Кожен пункт можна запускати повторно: старі результати перезаписуються.
public static class Lab5EnemyBuilder
{
    private const string ArtFolder = "Assets/Art/Enemies";
    private const string AnimFolder = "Assets/Art/Enemies/Animations";
    private const string DataFolder = "Assets/Data/Enemies";
    private const string ControllerPath = AnimFolder + "/Enemy.controller";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
    private const string NoFrictionPath = "Assets/Settings/PlayerNoFriction.physicsMaterial2D";

    // ---------------------------------------------------------------- 1
    [MenuItem("Tools/Lab 5/1. Build Enemy Prefab")]
    public static void BuildPrefabs()
    {
        // Ассет Walker створено з назвою за замовчуванням — приводимо до схеми Enemy_<Тип>
        string oldWalker = DataFolder + "/EnemyStatsEnemy_Walker.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(oldWalker) != null)
            AssetDatabase.RenameAsset(oldWalker, "Enemy_Walker");

        EnsureFolder(AnimFolder);
        var controller = BuildAnimator();
        if (controller == null) return;

        var projectile = BuildProjectile();
        BuildEnemy(controller, projectile);

        AssetDatabase.SaveAssets();
        Debug.Log("Lab 5: створено анімації, Enemy.controller, Projectile.prefab і Enemy.prefab.");
    }

    private static AnimatorController BuildAnimator()
    {
        var idle = MakeClip("enemy_idle", "Enemy_Idle", 4, true);
        var run = MakeClip("enemy_run", "Enemy_Run", 10, true);
        var tele = MakeClip("enemy_telegraph", "Enemy_Telegraph", 6, false);
        var hurt = MakeClip("enemy_hurt", "Enemy_Hurt", 10, false);
        var die = MakeClip("enemy_die", "Enemy_Die", 8, false);
        if (idle == null || run == null || tele == null || hurt == null || die == null) return null;

        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Telegraph", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(300, 0));
        var sRun = sm.AddState("Run", new Vector3(550, 0));
        var sTele = sm.AddState("Telegraph", new Vector3(300, 150));
        var sHurt = sm.AddState("Hurt", new Vector3(550, 150));
        var sDie = sm.AddState("Die", new Vector3(800, 150));
        sIdle.motion = idle; sRun.motion = run; sTele.motion = tele; sHurt.motion = hurt; sDie.motion = die;
        sm.defaultState = sIdle;

        // Idle ↔ Run — за швидкістю, без очікування кінця кліпу
        Link(sIdle, sRun).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        Link(sRun, sIdle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Any State → тригери. Die додаємо першим: він має пріоритет
        AnyLink(sm, sDie, "Die");
        AnyLink(sm, sHurt, "Hurt");
        AnyLink(sm, sTele, "Telegraph");

        // Telegraph і Hurt після одного програвання повертаються в Idle
        ExitLink(sTele, sIdle);
        ExitLink(sHurt, sIdle);
        // Die — кінцевий стан, виходу немає

        EditorUtility.SetDirty(ac);
        return ac;
    }

    private static AnimationClip MakeClip(string sheet, string clipName, float fps, bool loop)
    {
        var frames = AssetDatabase.LoadAllAssetsAtPath($"{ArtFolder}/{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        if (frames.Length == 0)
        {
            Debug.LogError($"Не знайдено нарізаних кадрів у {ArtFolder}/{sheet}.png (Sprite Mode Multiple, 44×52).");
            return null;
        }

        var clip = new AnimationClip { name = clipName, frameRate = fps };
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        // Останній кадр повторюємо, щоб він тривав повний такт
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[^1] };

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = $"{AnimFolder}/{clipName}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        return t;
    }

    private static void AnyLink(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void ExitLink(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
    }

    private static Projectile BuildProjectile()
    {
        var go = new GameObject("Projectile");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/projectile.png");
        SetSorting(sr, 1);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;     // Kinematic не отримує тригерів від статичної землі
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.2f;

        var p = go.AddComponent<Projectile>();
        var so = new SerializedObject(p);
        so.FindProperty("targetLayers").intValue = LayerMask.GetMask("Player", "Ground");
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<Projectile>();
    }

    private static void BuildEnemy(AnimatorController controller, Projectile projectile)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        var root = new GameObject("Enemy") { layer = enemyLayer };

        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = root.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Vertical;
        col.size = new Vector2(0.8f, 2.4f);
        col.offset = new Vector2(0f, 1.2f);
        // Тіло ворога не штовхає гравця: урон іде лише через атаку
        col.excludeLayers = LayerMask.GetMask("Player");
        col.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        Transform Child(string n, float x, float y)
        {
            var c = new GameObject(n) { layer = enemyLayer }.transform;
            c.SetParent(root.transform, false);
            c.localPosition = new Vector3(x, y, 0f);
            return c;
        }

        var visual = Child("Visual", 0f, 0f);
        var sr = visual.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath($"{ArtFolder}/enemy_idle.png").OfType<Sprite>()
            .OrderBy(s => s.name).FirstOrDefault();
        SetSorting(sr, 0);
        visual.gameObject.AddComponent<Animator>().runtimeAnimatorController = controller;

        var groundCheck = Child("GroundCheck", 0f, 0f);
        var edgeCheck = Child("EdgeCheck", 0.6f, -0.25f);
        var wallCheck = Child("WallCheck", 0.6f, 1.2f);
        var attackPoint = Child("AttackPoint", 0.8f, 1.2f);

        var ec = root.AddComponent<EnemyController>();
        var so = new SerializedObject(ec);
        so.FindProperty("stats").objectReferenceValue = LoadStats("Walker");
        so.FindProperty("groundCheck").objectReferenceValue = groundCheck;
        so.FindProperty("edgeCheck").objectReferenceValue = edgeCheck;
        so.FindProperty("wallCheck").objectReferenceValue = wallCheck;
        so.FindProperty("attackPoint").objectReferenceValue = attackPoint;
        so.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground");
        so.FindProperty("damageableLayers").intValue = LayerMask.GetMask("Player");
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
        so.ApplyModifiedPropertiesWithoutUndo();

        var eh = root.AddComponent<EnemyHealth>();
        var hso = new SerializedObject(eh);
        hso.FindProperty("controller").objectReferenceValue = ec;
        hso.FindProperty("hitbox").objectReferenceValue = col;
        hso.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        Object.DestroyImmediate(root);
    }

    // ---------------------------------------------------------------- 2
    [MenuItem("Tools/Lab 5/2. Place Enemies and Setup Player")]
    public static void PlaceEnemies()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefab == null) { Debug.LogError("Спершу виконайте Tools → Lab 5 → 1. Build Enemy Prefab."); return; }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("На сцені немає об'єкта з тегом Player."); return; }

        var scene = player.scene;

        // Прибираємо ручні спроби зібрати ворога (не префаб) — їх замінять екземпляри префаба
        foreach (var ec in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!PrefabUtility.IsPartOfPrefabInstance(ec.gameObject))
                Undo.DestroyObjectImmediate(ec.gameObject);

        // Група Enemies усередині GAMEPLAY (поруч із гравцем)
        Transform parent = player.transform.parent;
        var group = parent != null ? parent.Find("Enemies") : null;
        if (group == null)
        {
            var g = GameObject.Find("Enemies");
            group = g != null ? g.transform : null;
        }
        if (group == null)
        {
            group = new GameObject("Enemies").transform;
            group.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Create Enemies group");
        }
        for (int i = group.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(group.GetChild(i).gameObject);

        // Розстановка за картою рівня: поверхня землі y = 3, уступ стрільця y = 5
        Spawn(prefab, group, "Walker", new Vector3(27f, 3f, 0f));   // біт 2: майданчик між перешкодами x 21–32
        Spawn(prefab, group, "Rusher", new Vector3(86f, 3f, 0f));   // арена: рівнина x 70–94
        Spawn(prefab, group, "Shooter", new Vector3(99f, 5f, 0f));  // арена: уступ x 95–102
        Spawn(prefab, group, "Tank", new Vector3(110f, 3f, 0f));    // арена: перед виходом x 103–118

        SetupPlayer(player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Lab 5: на рівні 4 вороги (Walker, Rusher, Shooter, Tank); гравцю додано PlayerHealth і PlayerStomp. Сцену збережено.");
    }

    private static void Spawn(GameObject prefab, Transform parent, string type, Vector3 pos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Place enemy");
        go.name = "Enemy_" + type;
        go.transform.position = pos;

        var so = new SerializedObject(go.GetComponent<EnemyController>());
        so.FindProperty("stats").objectReferenceValue = LoadStats(type);
        so.ApplyModifiedProperties();
    }

    private static void SetupPlayer(GameObject player)
    {
        Undo.RegisterFullObjectHierarchyUndo(player, "Setup player combat");
        player.layer = LayerMask.NameToLayer("Player");

        // Не «??»: в редакторі відсутній компонент — це «фальшивий null» Unity
        var health = player.GetComponent<PlayerHealth>();
        if (health == null) health = Undo.AddComponent<PlayerHealth>(player);
        var hso = new SerializedObject(health);
        hso.FindProperty("respawn").objectReferenceValue = player.GetComponent<PlayerRespawn>();
        hso.FindProperty("maxHealth").intValue = 50;
        hso.ApplyModifiedProperties();

        var stompPoint = player.transform.Find("StompPoint");
        if (stompPoint == null)
        {
            stompPoint = new GameObject("StompPoint").transform;
            Undo.RegisterCreatedObjectUndo(stompPoint.gameObject, "Create StompPoint");
            stompPoint.SetParent(player.transform, false);
        }
        stompPoint.localPosition = Vector3.zero;   // під ступнями: опорна точка гравця — низ

        var stomp = player.GetComponent<PlayerStomp>();
        if (stomp == null) stomp = Undo.AddComponent<PlayerStomp>(player);
        var sso = new SerializedObject(stomp);
        sso.FindProperty("stompPoint").objectReferenceValue = stompPoint;
        sso.FindProperty("enemyLayer").intValue = LayerMask.GetMask("Enemy");
        sso.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------- 3
    private const string SlashFolder = "Assets/Art/Effects";
    private const string SwingSfxPath = "Assets/Audio/sword_swing.wav";

    [MenuItem("Tools/Lab 5/3. Setup Sword Attack")]
    public static void SetupSword()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("На сцені немає об'єкта з тегом Player."); return; }

        // Кадри дуги: піксель-арт, PPU 16, опорна точка — центр дуги біля плеча
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = $"{SlashFolder}/slash_{i}.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogError($"Не знайдено {path}."); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Custom;
            st.spritePivot = new Vector2(0.1f, 0.5f);
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup sword attack");

        Transform Child(string n, Vector3 pos)
        {
            var c = player.transform.Find(n);
            if (c == null)
            {
                c = new GameObject(n).transform;
                Undo.RegisterCreatedObjectUndo(c.gameObject, "Create " + n);
                c.SetParent(player.transform, false);
            }
            c.localPosition = pos;
            return c;
        }

        var attackPoint = Child("SwordPoint", new Vector3(1.1f, 1.3f, 0f));
        var slashT = Child("Slash", new Vector3(0.3f, 1.3f, 0f));
        var slash = slashT.GetComponent<SpriteRenderer>();
        if (slash == null) slash = Undo.AddComponent<SpriteRenderer>(slashT.gameObject);
        slash.sprite = frames[1];
        slash.sortingLayerName = "Player";
        slash.sortingOrder = 5;          // поверх гравця

        var attack = player.GetComponent<PlayerAttack>();
        if (attack == null) attack = Undo.AddComponent<PlayerAttack>(player);
        var so = new SerializedObject(attack);
        so.FindProperty("body").objectReferenceValue = player.GetComponent<SpriteRenderer>();
        so.FindProperty("attackPoint").objectReferenceValue = attackPoint;
        so.FindProperty("enemyLayer").intValue = LayerMask.GetMask("Enemy");
        so.FindProperty("slash").objectReferenceValue = slash;
        var arr = so.FindProperty("slashFrames");
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.FindProperty("audioSource").objectReferenceValue = player.GetComponent<AudioSource>();
        so.FindProperty("swingSfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SwingSfxPath);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        Debug.Log("Lab 5: гравцю додано удар мечем (ЛКМ або J). Сцену збережено.");
    }

    // ---------------- допоміжне ----------------
    private static EnemyStatsSO LoadStats(string type)
    {
        var s = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>($"{DataFolder}/Enemy_{type}.asset");
        if (s == null) Debug.LogError($"Не знайдено {DataFolder}/Enemy_{type}.asset.");
        return s;
    }

    private static void SetSorting(SpriteRenderer sr, int order)
    {
        sr.sortingLayerName = SortingLayer.layers.Any(l => l.name == "Enemies") ? "Enemies" : "Player";
        sr.sortingOrder = order;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
