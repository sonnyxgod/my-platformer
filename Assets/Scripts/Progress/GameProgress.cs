using System.Collections.Generic;
using UnityEngine;

// Статичний клас: жодного об'єкта в сцені, нічого не треба перетягувати,
// і ніщо не знищується при завантаженні наступного рівня
public static class GameProgress
{
    public static int Coins { get; private set; }
    public static int LevelIndex { get; set; }

    // Остання контрольна точка поточного рівня
    public static bool HasCheckpoint { get; private set; }
    public static Vector2 CheckpointPosition { get; private set; }

    // Ідентифікатори вже зібраних душ: «номер сцени:ім'я об'єкта»
    private static readonly HashSet<string> collected = new HashSet<string>();

    public static void AddCoins(int amount) => Coins += amount;

    public static void MarkCollected(string id) => collected.Add(id);
    public static bool IsCollected(string id) => collected.Contains(id);

    public static void SetCheckpoint(Vector2 position)
    {
        HasCheckpoint = true;
        CheckpointPosition = position;
    }

    // Новий рівень або рестарт — починаємо зі старту
    public static void ClearCheckpoint() => HasCheckpoint = false;

    public static void Apply(SaveData data)
    {
        Coins = data.coins;
        LevelIndex = data.levelIndex;
        HasCheckpoint = data.hasCheckpoint;
        CheckpointPosition = new Vector2(data.checkpointX, data.checkpointY);
        collected.Clear();
        collected.UnionWith(data.collected);
    }

    public static SaveData ToData() => new SaveData
    {
        coins = Coins,
        levelIndex = LevelIndex,
        hasCheckpoint = HasCheckpoint,
        checkpointX = CheckpointPosition.x,
        checkpointY = CheckpointPosition.y,
        collected = new List<string>(collected),
    };

    public static void Reset()
    {
        Coins = 0;
        LevelIndex = 0;
        HasCheckpoint = false;
        CheckpointPosition = Vector2.zero;
        collected.Clear();
    }
}
