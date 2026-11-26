using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public const int CurrentVersion = 3;

    // ЗАВЖДИ перше поле. Без нього старий сейв неможливо ані впізнати,
    // ані полагодити: невідомо, що саме в ньому лежить
    public int version = CurrentVersion;

    public int coins;       // був уже у версії 1
    public int levelIndex;  // з'явився у версії 2, коли рівнів стало більше ніж один

    // Версія 3: «Продовжити» повертає на контрольну точку, а не на старт,
    // і вже зібрані душі не з'являються на рівні вдруге
    public bool hasCheckpoint;
    public float checkpointX;
    public float checkpointY;
    public List<string> collected = new List<string>();
}
