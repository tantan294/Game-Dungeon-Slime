using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lưu tiến trình chơi bằng PlayerPrefs:
/// - Màn đã qua (mỗi màn một khóa), màn chơi gần nhất.
/// - Màn N được mở khi đã qua màn N-1 (màn 1 luôn mở).
/// Màn chơi là các Scene tên "Level NN" trong Build Settings; Scene "Menu" là màn hình chính.
/// </summary>
public static class LevelProgress
{
    public const string MenuScene = "Menu";

    private const string LevelPrefix = "Level ";
    private const string ClearedKey = "JellyEscape.Cleared.";
    private const string LastPlayedKey = "JellyEscape.LastPlayed";
    private const string TitlesResource = "LevelTitles"; // Resources/LevelTitles.txt, mỗi dòng một tên màn (JellyLevelBuilder ghi ra)

    private static string[] titles;

    /// <summary>
    /// Số màn chơi có trong Build Settings
    /// </summary>
    public static int LevelCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (GetLevelNumber(name) > 0) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Số thứ tự màn từ tên Scene ("Level 07" -> 7), 0 nếu không phải màn chơi
    /// </summary>
    public static int GetLevelNumber(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith(LevelPrefix)) return 0;
        return int.TryParse(sceneName.Substring(LevelPrefix.Length), out int number) ? number : 0;
    }

    public static string GetSceneName(int levelNumber)
    {
        return LevelPrefix + levelNumber.ToString("00");
    }

    /// <summary>
    /// Tên hiển thị của màn (rỗng nếu không có)
    /// </summary>
    public static string GetTitle(int levelNumber)
    {
        if (titles == null)
        {
            TextAsset asset = Resources.Load<TextAsset>(TitlesResource);
            titles = asset != null ? asset.text.Split(new[] { (char)13, (char)10 }, System.StringSplitOptions.RemoveEmptyEntries) : new string[0];
        }
        return levelNumber >= 1 && levelNumber <= titles.Length ? titles[levelNumber - 1].Trim() : "";
    }

    public static bool IsCleared(int levelNumber)
    {
        return PlayerPrefs.GetInt(ClearedKey + levelNumber, 0) == 1;
    }

    public static bool IsUnlocked(int levelNumber)
    {
        return levelNumber <= 1 || IsCleared(levelNumber) || IsCleared(levelNumber - 1);
    }

    public static void MarkCleared(int levelNumber)
    {
        if (levelNumber <= 0) return;
        PlayerPrefs.SetInt(ClearedKey + levelNumber, 1);
        PlayerPrefs.Save();
    }

    public static int LastPlayed
    {
        get => PlayerPrefs.GetInt(LastPlayedKey, 0);
        set
        {
            PlayerPrefs.SetInt(LastPlayedKey, value);
            PlayerPrefs.Save();
        }
    }

    public static int ClearedCount
    {
        get
        {
            int count = 0;
            for (int i = 1; i <= LevelCount; i++)
            {
                if (IsCleared(i)) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Màn nên chơi tiếp: màn chưa qua đầu tiên (đã qua hết thì là màn chơi gần nhất)
    /// </summary>
    public static int NextLevelToPlay
    {
        get
        {
            int count = LevelCount;
            for (int i = 1; i <= count; i++)
            {
                if (!IsCleared(i)) return i;
            }
            return Mathf.Clamp(LastPlayed, 1, Mathf.Max(1, count));
        }
    }

    public static void ResetAll()
    {
        for (int i = 1; i <= LevelCount; i++)
        {
            PlayerPrefs.DeleteKey(ClearedKey + i);
        }
        PlayerPrefs.DeleteKey(LastPlayedKey);
        PlayerPrefs.Save();
    }
}
