using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dựng các màn chơi Jelly Escape từ bản đồ chữ trong JellyLevels.txt (menu: Jelly Escape > Tạo màn chơi).
/// Mỗi ô = 0.5 đơn vị (bằng 1 nửa Jelly vuông). Ký hiệu:
///   '#' tường (Layer Obstacle)   '.' sàn   ' ' ngoài màn
///   'E' cửa Exit (đặt ở lỗ hổng tường bao)   '^' gai   'S' vị trí Jelly (khối 2x2 ô)
///   '1'..'3' cửa chặn theo kênh (mở khi mọi nút cùng kênh được nhấn cùng lúc)
///   chữ thường = nút nhấn, khai báo trong màn bằng dòng "! a 1 Tall" (ký tự, kênh, hình dạng PlateShape)
/// Dùng lại sprite có sẵn: tường Level01_151, sàn Blocks_1, gai Blocks_6, cửa Square vàng như màn cũ.
/// </summary>
public static class JellyLevelBuilder
{
    private const string LevelFile = "Assets/Editor/JellyLevels.txt";
    private const string SceneFolder = "Assets/Scenes/Jelly";
    private const string TemplateScene = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    private const string SlimePrefab = "Assets/Prefabs/Slime.prefab";
    private const string ExitSpriteGuid = "311925a002f4447b3a28927169b83ea6";
    private const string TitlesFile = "Assets/Resources/LevelTitles.txt";

    private const float Cell = 0.5f;
    private const int ObstacleLayer = 6;
    private const float SpikeInset = 0.1f; // Collider gai nhỏ hơn ô 0.1 mỗi phía để lướt sát cạnh không bị chết oan
    private const float ExitInset = 0.1f;  // Collider cửa lùi vào 0.1 để chạm mép tường bao không tính là qua màn

    private static readonly Color FloorTint = new Color(0.42f, 0.44f, 0.55f, 1f);
    private static readonly Color ExitTint = new Color(1f, 0.93f, 0.43f, 1f);
    private static readonly Color BackgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);
    private static readonly Color[] ChannelColors =
    {
        new Color(1f, 0.55f, 0.2f, 1f),   // kênh 1: cam
        new Color(0.3f, 0.8f, 1f, 1f),    // kênh 2: xanh dương
        new Color(0.45f, 0.95f, 0.4f, 1f) // kênh 3: xanh lá
    };

    private struct PlateInfo
    {
        public int Channel;
        public PlateShape Shape;
    }

    private class LevelData
    {
        public string Title;
        public List<string> Rows = new List<string>();
        public Dictionary<char, PlateInfo> Plates = new Dictionary<char, PlateInfo>();
    }

    [MenuItem("Jelly Escape/Tạo màn chơi")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        List<LevelData> levels = LoadLevels();
        if (levels.Count == 0)
        {
            Debug.LogError("[JellyLevelBuilder] Không đọc được màn nào trong " + LevelFile);
            return;
        }

        Sprite wallSprite = LoadSprite("Assets/PixelPack03_Free/Level01.png", "Level01_151");
        Sprite floorSprite = LoadSprite("Assets/PixelPack03_Free/Blocks.png", "Blocks_1");
        Sprite spikeSprite = LoadSprite("Assets/PixelPack03_Free/Blocks.png", "Blocks_6");
        Sprite exitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(ExitSpriteGuid));
        if (exitSprite == null) exitSprite = LoadSprite("Assets/PixelPack03_Free/Blocks.png", "Blocks_0");
        GameObject slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefab);

        if (wallSprite == null || floorSprite == null || spikeSprite == null || slimePrefab == null)
        {
            Debug.LogError("[JellyLevelBuilder] Thiếu sprite hoặc prefab Slime, dừng dựng màn.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "Jelly");
        }

        // Scene Menu luôn đứng đầu Build Settings (game khởi động vào Menu)
        var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BuildMenuScene(), true) };
        for (int i = 0; i < levels.Count; i++)
        {
            string sceneName = SceneName(i);
            string nextName = i + 1 < levels.Count ? SceneName(i + 1) : LevelProgress.MenuScene;
            string path = SceneFolder + "/" + sceneName + ".unity";

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(TemplateScene, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            BuildLevel(levels[i], nextName, wallSprite, floorSprite, spikeSprite, exitSprite, slimePrefab);

            EditorSceneManager.SaveScene(scene);
            buildScenes.Add(new EditorBuildSettingsScene(path, true));
            Debug.Log($"[JellyLevelBuilder] Đã dựng {sceneName}: {levels[i].Title}");
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        WriteTitles(levels);
        EditorSceneManager.OpenScene(SceneFolder + "/" + LevelProgress.MenuScene + ".unity", OpenSceneMode.Single);
        Debug.Log($"[JellyLevelBuilder] Hoàn tất Menu + {levels.Count} màn, đã cập nhật Build Settings.");
    }

    /// <summary>
    /// Scene Menu: Camera nền tối + MainMenu (giao diện tự dựng khi chạy)
    /// </summary>
    private static string BuildMenuScene()
    {
        string path = SceneFolder + "/" + LevelProgress.MenuScene + ".unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(TemplateScene, path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        new GameObject("MainMenu").AddComponent<MainMenu>();
        SetupCamera(1, 1);

        EditorSceneManager.SaveScene(scene);
        return path;
    }

    /// <summary>
    /// Ghi tên các màn (mỗi dòng một tên) cho Menu và HUD đọc lúc chạy
    /// </summary>
    private static void WriteTitles(List<LevelData> levels)
    {
        var titles = new List<string>();
        foreach (var level in levels) titles.Add(level.Title);
        File.WriteAllLines(TitlesFile, titles);
        AssetDatabase.ImportAsset(TitlesFile);
    }

    private static string SceneName(int index)
    {
        return "Level " + (index + 1).ToString("00");
    }

    private static List<LevelData> LoadLevels()
    {
        var levels = new List<LevelData>();
        if (!File.Exists(LevelFile)) return levels;

        LevelData current = null;
        foreach (string raw in File.ReadAllLines(LevelFile))
        {
            if (raw.StartsWith("="))
            {
                current = new LevelData { Title = raw.Substring(1).Trim() };
                levels.Add(current);
            }
            else if (current != null && raw.StartsWith("!"))
            {
                // "! a 1 Tall" -> nút 'a' thuộc kênh 1, yêu cầu hình Tall
                string[] parts = raw.Substring(1).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && int.TryParse(parts[1], out int channel)
                    && System.Enum.TryParse(parts[2], out PlateShape shape))
                {
                    current.Plates[parts[0][0]] = new PlateInfo { Channel = channel, Shape = shape };
                }
                else
                {
                    Debug.LogWarning("[JellyLevelBuilder] Dòng khai báo nút không hợp lệ: " + raw);
                }
            }
            else if (current != null && raw.Trim().Length > 0)
            {
                current.Rows.Add(raw.TrimEnd());
            }
        }
        return levels;
    }

    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (o is Sprite s && s.name == spriteName) return s;
        }
        Debug.LogWarning($"[JellyLevelBuilder] Không tìm thấy sprite {spriteName} trong {assetPath}");
        return null;
    }

    private static void BuildLevel(LevelData level, string nextSceneName, Sprite wallSprite, Sprite floorSprite,
        Sprite spikeSprite, Sprite exitSprite, GameObject slimePrefab)
    {
        int height = level.Rows.Count;
        int width = 0;
        foreach (string row in level.Rows) width = Mathf.Max(width, row.Length);

        Transform walls = new GameObject("Walls").transform;
        Transform floor = new GameObject("Floor").transform;
        Transform hazards = new GameObject("Hazards").transform;
        Transform switches = new GameObject("Switches").transform;

        var spawnCells = new List<Vector2Int>();
        var exitCells = new HashSet<Vector2Int>();

        for (int r = 0; r < height; r++)
        {
            string row = level.Rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                char ch = row[c];
                if (ch == ' ') continue;

                Vector3 pos = CellCenter(c, r);
                if (ch == '#')
                {
                    CreateSolidTile("Wall", walls, pos, wallSprite, Color.white);
                    continue;
                }

                CreateTile("Floor", floor, pos, floorSprite, FloorTint, -10);

                if (ch >= '1' && ch <= '3')
                {
                    CreateDoor(ch - '0', switches, pos, wallSprite);
                }
                else if (level.Plates.TryGetValue(ch, out PlateInfo plate))
                {
                    CreatePlate(plate, switches, pos, floorSprite, exitSprite);
                }
                else if (ch == '^')
                {
                    GameObject spike = CreateTile("Spike", hazards, pos, spikeSprite, Color.white, 0);
                    BoxCollider2D col = spike.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = Vector2.one * (Cell - SpikeInset * 2f) / spike.transform.localScale.x;
                    spike.AddComponent<HazardSpike>();
                }
                else if (ch == 'E')
                {
                    exitCells.Add(new Vector2Int(c, r));
                }
                else if (ch == 'S')
                {
                    spawnCells.Add(new Vector2Int(c, r));
                }
            }
        }

        foreach (var group in GroupCells(exitCells))
        {
            CreateExit(group, nextSceneName, exitSprite);
        }

        // Jelly đặt tại tâm khối 'S'
        Vector3 spawn = Vector3.zero;
        foreach (var p in spawnCells) spawn += CellCenter(p.x, p.y);
        if (spawnCells.Count > 0) spawn /= spawnCells.Count;
        GameObject slime = (GameObject)PrefabUtility.InstantiatePrefab(slimePrefab);
        slime.transform.position = new Vector3(spawn.x, spawn.y, 0f);

        SetupCamera(width, height);
    }

    private static Color ChannelColor(int channel)
    {
        return ChannelColors[Mathf.Clamp(channel - 1, 0, ChannelColors.Length - 1)];
    }

    /// <summary>
    /// Ô chắn đường (tường / cửa): Layer Obstacle + BoxCollider2D khớp sprite
    /// </summary>
    private static GameObject CreateSolidTile(string name, Transform parent, Vector3 pos, Sprite sprite, Color tint)
    {
        GameObject tile = CreateTile(name, parent, pos, sprite, tint, 1);
        tile.layer = ObstacleLayer;
        tile.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
        return tile;
    }

    private static void CreateDoor(int channel, Transform parent, Vector3 pos, Sprite wallSprite)
    {
        GameObject door = CreateSolidTile("Door " + channel, parent, pos, wallSprite, ChannelColor(channel));

        PlateDoor plateDoor = door.AddComponent<PlateDoor>();
        var so = new SerializedObject(plateDoor);
        so.FindProperty("channel").intValue = channel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePlate(PlateInfo info, Transform parent, Vector3 pos, Sprite baseSprite, Sprite iconSprite)
    {
        Color color = ChannelColor(info.Channel);
        Color idle = Color.Lerp(color, Color.black, 0.45f);
        GameObject plate = CreateTile("Plate " + info.Channel + " " + info.Shape, parent, pos, baseSprite, idle, -5);

        // Biểu tượng hình dạng yêu cầu (tỉ lệ đúng như hình Jelly), Any = chấm nhỏ
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(plate.transform, false);
        icon.transform.position = pos;
        SlimeShape? shape = info.Shape.ToSlimeShape();
        Vector2 dims = shape.HasValue ? SlimeShapeShifter.GetDimensions(shape.Value) : Vector2.one;
        float fit = (shape.HasValue ? Cell * 0.8f : 0.12f) / Mathf.Max(dims.x, dims.y);
        Vector2 iconSize = dims * fit;
        Vector2 spriteSize = iconSprite.bounds.size;
        icon.transform.localScale = new Vector3(iconSize.x / spriteSize.x / plate.transform.localScale.x,
            iconSize.y / spriteSize.y / plate.transform.localScale.y, 1f);
        SpriteRenderer iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = iconSprite;
        iconRenderer.color = new Color(1f, 1f, 1f, 0.9f);
        iconRenderer.sortingOrder = -4;

        PressurePlate pressurePlate = plate.AddComponent<PressurePlate>();
        var so = new SerializedObject(pressurePlate);
        so.FindProperty("channel").intValue = info.Channel;
        so.FindProperty("requiredShape").enumValueIndex = (int)info.Shape;
        so.FindProperty("plateRenderer").objectReferenceValue = plate.GetComponent<SpriteRenderer>();
        so.FindProperty("idleColor").colorValue = idle;
        so.FindProperty("pressedColor").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 CellCenter(int c, int r)
    {
        return new Vector3(c * Cell + Cell * 0.5f, -(r * Cell + Cell * 0.5f), 0f);
    }

    private static GameObject CreateTile(string name, Transform parent, Vector3 pos, Sprite sprite, Color tint, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        float scale = Cell / sprite.bounds.size.x;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = order;
        return go;
    }

    private static List<List<Vector2Int>> GroupCells(HashSet<Vector2Int> cells)
    {
        var groups = new List<List<Vector2Int>>();
        var left = new HashSet<Vector2Int>(cells);
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (left.Count > 0)
        {
            var e = left.GetEnumerator();
            e.MoveNext();
            Vector2Int start = e.Current;
            left.Remove(start);

            var group = new List<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                Vector2Int p = stack.Pop();
                group.Add(p);
                foreach (var d in dirs)
                {
                    if (left.Remove(p + d)) stack.Push(p + d);
                }
            }
            groups.Add(group);
        }
        return groups;
    }

    private static void CreateExit(List<Vector2Int> cells, string nextSceneName, Sprite exitSprite)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        foreach (var p in cells)
        {
            Vector3 center = CellCenter(p.x, p.y);
            min = Vector3.Min(min, center - new Vector3(Cell, Cell, 0f) * 0.5f);
            max = Vector3.Max(max, center + new Vector3(Cell, Cell, 0f) * 0.5f);
        }
        Vector2 size = max - min;

        GameObject exit = new GameObject("Exit");
        exit.transform.position = (min + max) * 0.5f;
        Vector2 spriteSize = exitSprite.bounds.size;
        exit.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);

        SpriteRenderer sr = exit.AddComponent<SpriteRenderer>();
        sr.sprite = exitSprite;
        sr.color = ExitTint;
        sr.sortingOrder = 0;

        BoxCollider2D col = exit.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        Vector2 inner = size - Vector2.one * (ExitInset * 2f);
        col.size = new Vector2(inner.x / exit.transform.localScale.x, inner.y / exit.transform.localScale.y);

        LevelExit levelExit = exit.AddComponent<LevelExit>();
        var so = new SerializedObject(levelExit);
        so.FindProperty("nextSceneName").stringValue = nextSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupCamera(int width, int height)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) return;

        float worldW = width * Cell;
        float worldH = height * Cell;
        // Chừa khoảng trên cho HUD (Level + Moves) ở góc trên
        cam.transform.position = new Vector3(worldW * 0.5f, -worldH * 0.5f + 0.4f, -10f);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(worldH * 0.5f + 1f, (worldW * 0.5f + 0.5f) / (16f / 9f));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
    }
}
