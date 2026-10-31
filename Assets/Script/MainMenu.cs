using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Màn hình chính (Scene "Menu"): Chơi tiếp / Chọn màn / Xóa tiến trình / Thoát.
/// Bảng chọn màn đọc tiến trình đã lưu (LevelProgress): màn đã qua, màn đang mở, màn còn khóa.
/// Giao diện dựng bằng code khi vào Scene.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private const int Columns = 5;

    private GameObject mainPanel;
    private GameObject levelPanel;
    private Text continueLabel;
    private Text progressText;
    private Text resetLabel;
    private Transform levelGrid;
    private readonly List<GameObject> levelButtons = new List<GameObject>();
    private bool confirmReset;

    private void Awake()
    {
        UIFactory.EnsureEventSystem();
        Transform canvas = UIFactory.CreateCanvas(gameObject, 100).transform;
        BuildMainPanel(canvas);
        BuildLevelPanel(canvas);
        ShowMain();
    }

    private void Update()
    {
        if (levelPanel.activeSelf && IsBackPressed()) ShowMain();
    }

    private static bool IsBackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif

        return false;
    }

    #region Dựng giao diện

    private void BuildMainPanel(Transform parent)
    {
        RectTransform root = UIFactory.CreateFill("MainPanel", parent);
        mainPanel = root.gameObject;

        RectTransform titleRect = UIFactory.CreateRect("Title", root, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1200, 150));
        Text title = UIFactory.CreateText(titleRect, "JELLY ESCAPE", 120, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        Shadow shadow = title.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.35f, 0.08f, 0.2f, 0.9f);
        shadow.effectDistance = new Vector2(6, -6);

        RectTransform subRect = UIFactory.CreateRect("Subtitle", root, new Vector2(0.5f, 1), new Vector2(0, -305), new Vector2(1200, 50));
        UIFactory.CreateText(subRect, "Trượt, biến hình và tách đôi để thoát khỏi mê cung", 30, UIFactory.TextDim);

        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = new Vector2(460, 76);
        Button cont = UIFactory.CreateButton(root, "Btn_Continue", "", center, new Vector2(0, 30), size,
            UIFactory.Accent, UIFactory.AccentHover, () => LoadLevel(LevelProgress.NextLevelToPlay), 28);
        continueLabel = cont.GetComponentInChildren<Text>();

        UIFactory.CreateButton(root, "Btn_LevelSelect", "CHỌN MÀN", center, new Vector2(0, -66), size,
            UIFactory.Button, UIFactory.ButtonHover, ShowLevels, 28);

        Button reset = UIFactory.CreateButton(root, "Btn_Reset", "", center, new Vector2(0, -162), size,
            UIFactory.Button, UIFactory.ButtonHover, OnResetClicked, 24);
        resetLabel = reset.GetComponentInChildren<Text>();

        UIFactory.CreateButton(root, "Btn_Quit", "THOÁT GAME", center, new Vector2(0, -258), size,
            UIFactory.Danger, UIFactory.DangerHover, Application.Quit, 28);

        RectTransform progressRect = UIFactory.CreateRect("Progress", root, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(800, 40));
        progressText = UIFactory.CreateText(progressRect, "", 24, UIFactory.TextDim);
    }

    private void BuildLevelPanel(Transform parent)
    {
        RectTransform root = UIFactory.CreateFill("LevelPanel", parent);
        levelPanel = root.gameObject;

        RectTransform titleRect = UIFactory.CreateRect("Title", root, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1000, 90));
        UIFactory.CreateText(titleRect, "CHỌN MÀN", 64, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

        RectTransform grid = UIFactory.CreateRect("Grid", root, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(Columns * 250, 720));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(230, 150);
        layout.spacing = new Vector2(20, 20);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        layout.childAlignment = TextAnchor.MiddleCenter;
        levelGrid = grid;

        UIFactory.CreateButton(root, "Btn_Back", "QUAY LẠI  (ESC)", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(360, 70),
            UIFactory.Button, UIFactory.ButtonHover, ShowMain, 26);
    }

    /// <summary>
    /// Dựng lại các ô màn theo tiến trình mới nhất
    /// </summary>
    private void RebuildLevelButtons()
    {
        foreach (var go in levelButtons) Destroy(go);
        levelButtons.Clear();

        int count = LevelProgress.LevelCount;
        int lastPlayed = LevelProgress.LastPlayed;
        for (int i = 1; i <= count; i++)
        {
            int level = i;
            bool cleared = LevelProgress.IsCleared(level);
            bool unlocked = LevelProgress.IsUnlocked(level);

            Color normal = cleared ? new Color(0.13f, 0.3f, 0.24f, 1f) : UIFactory.Button;
            Color hover = cleared ? new Color(0.18f, 0.42f, 0.33f, 1f) : UIFactory.ButtonHover;
            Button btn = UIFactory.CreateButton(levelGrid, "Level_" + level, "", Vector2.zero, Vector2.zero, Vector2.zero,
                normal, hover, () => LoadLevel(level));
            btn.interactable = unlocked;
            Destroy(btn.GetComponentInChildren<Text>().gameObject);

            // Viền hồng cho màn chơi gần nhất
            if (level == lastPlayed && unlocked)
            {
                Outline outline = btn.gameObject.AddComponent<Outline>();
                outline.effectColor = UIFactory.Accent;
                outline.effectDistance = new Vector2(4, -4);
            }

            Color textColor = unlocked ? UIFactory.TextMain : UIFactory.TextDim;
            RectTransform numRect = UIFactory.CreateRect("Number", btn.transform, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(210, 60));
            UIFactory.CreateText(numRect, level.ToString(), 46, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform nameRect = UIFactory.CreateRect("Name", btn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(214, 34));
            UIFactory.CreateText(nameRect, unlocked ? LevelProgress.GetTitle(level) : "", 19, textColor);

            string status = cleared ? "ĐÃ QUA" : unlocked ? "CHƯA QUA" : "ĐANG KHÓA";
            Color statusColor = cleared ? UIFactory.Success : unlocked ? UIFactory.Accent : UIFactory.TextDim;
            RectTransform statusRect = UIFactory.CreateRect("Status", btn.transform, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(210, 30));
            UIFactory.CreateText(statusRect, status, 17, statusColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            levelButtons.Add(btn.gameObject);
        }
    }

    #endregion

    #region Điều hướng

    private void ShowMain()
    {
        confirmReset = false;
        levelPanel.SetActive(false);
        mainPanel.SetActive(true);
        RefreshMain();
    }

    private void ShowLevels()
    {
        mainPanel.SetActive(false);
        levelPanel.SetActive(true);
        RebuildLevelButtons();
    }

    private void RefreshMain()
    {
        int cleared = LevelProgress.ClearedCount;
        int next = LevelProgress.NextLevelToPlay;
        continueLabel.text = cleared == 0 ? "BẮT ĐẦU CHƠI" : $"CHƠI TIẾP  -  MÀN {next}";
        progressText.text = $"Đã qua {cleared} / {LevelProgress.LevelCount} màn";
        resetLabel.text = confirmReset ? "BẤM LẦN NỮA ĐỂ XÓA" : "XÓA TIẾN TRÌNH";
    }

    /// <summary>
    /// Xóa tiến trình cần bấm 2 lần để tránh lỡ tay
    /// </summary>
    private void OnResetClicked()
    {
        if (confirmReset)
        {
            LevelProgress.ResetAll();
            confirmReset = false;
        }
        else
        {
            confirmReset = true;
        }
        RefreshMain();
    }

    private static void LoadLevel(int level)
    {
        if (!LevelProgress.IsUnlocked(level)) return;
        string scene = LevelProgress.GetSceneName(level);
        if (SceneFader.Instance != null) SceneFader.Instance.FadeToScene(scene, 0.35f);
        else SceneManager.LoadScene(scene);
    }

    #endregion
}
