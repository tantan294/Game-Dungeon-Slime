using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Giao diện trong màn chơi (tự tạo, DontDestroyOnLoad):
/// - HUD góc trên trái: số màn + tên màn. Góc trên phải: nút Chơi lại [R] / Tạm dừng [Esc].
/// - Thanh gợi ý phím ở đáy màn hình (đổi nội dung khi Jelly đang tách đôi).
/// - Hộp thoại Tạm dừng: Tiếp tục / Chơi lại / Chọn màn (về Menu) / Thoát game.
/// HUD tự ẩn ở Scene Menu.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private const string HintWhole = "WASD / PHÍM MŨI TÊN: TRƯỢT     F: TÁCH ĐÔI     R: CHƠI LẠI     ESC: TẠM DỪNG";
    private const string HintSplit = "NỬA HỒNG: WASD     NỬA XANH: PHÍM MŨI TÊN     F: GỘP LẠI (KHI HAI NỬA CHẠM NHAU)";

    private Canvas canvas;
    private Text levelNumberText;
    private Text levelTitleText;
    private Text hintText;
    private Text pauseSubtitleText;
    private GameObject pauseModalRoot;
    private RectTransform pauseDialogRect;
    private bool isPaused;
    private bool showingSplitHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        Instance = FindFirstObjectByType<UIManager>();
        if (Instance == null)
        {
            Instance = new GameObject("[UIManager]").AddComponent<UIManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        UIFactory.EnsureEventSystem();
        canvas = UIFactory.CreateCanvas(gameObject, 500); // Dưới SceneFader (9999), trên thế giới game
        BuildHUD(canvas.transform);
        BuildPauseModal(canvas.transform);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        RefreshForScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForScene(scene);
    }

    private void Update()
    {
        if (!canvas.enabled) return;

        if (IsEscapePressed()) TogglePause();

        bool split = SlimeMovement.ActiveSlimes.Count > 1;
        if (split != showingSplitHint)
        {
            showingSplitHint = split;
            hintText.text = split ? HintSplit : HintWhole;
        }
    }

    private static bool IsEscapePressed()
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

    /// <summary>
    /// Cập nhật HUD theo Scene: ẩn ở Menu, hiện số màn + tên màn ở màn chơi
    /// </summary>
    private void RefreshForScene(Scene scene)
    {
        HideAllModals();

        int levelNumber = LevelProgress.GetLevelNumber(scene.name);
        canvas.enabled = levelNumber > 0;
        if (levelNumber <= 0) return;

        string title = LevelProgress.GetTitle(levelNumber);
        if (string.IsNullOrEmpty(title)) title = scene.name;

        levelNumberText.text = $"MÀN {levelNumber} / {LevelProgress.LevelCount}";
        levelTitleText.text = title;
        pauseSubtitleText.text = $"Màn {levelNumber}: {title}";
        showingSplitHint = false;
        hintText.text = HintWhole;
    }

    #region Dựng giao diện

    private void BuildHUD(Transform parent)
    {
        // Thẻ tên màn (góc trên trái) với vạch nhấn màu hồng
        RectTransform card = UIFactory.CreateRect("HUD_LevelCard", parent, new Vector2(0, 1), new Vector2(30, -30), new Vector2(440, 100));
        UIFactory.AddImage(card, UIFactory.PanelLight);

        RectTransform bar = UIFactory.CreateRect("AccentBar", card, new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 100));
        UIFactory.AddImage(bar, UIFactory.Accent);

        RectTransform numberRect = UIFactory.CreateRect("LevelNumber", card, new Vector2(0, 1), new Vector2(28, -12), new Vector2(400, 30));
        levelNumberText = UIFactory.CreateText(numberRect, "MÀN 1", 20, UIFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);

        RectTransform titleRect = UIFactory.CreateRect("LevelTitle", card, new Vector2(0, 0), new Vector2(28, 12), new Vector2(400, 50));
        levelTitleText = UIFactory.CreateText(titleRect, "", 34, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);

        // Cụm nút góc trên phải
        UIFactory.CreateButton(parent, "Btn_Pause", "TẠM DỪNG  (ESC)", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(250, 64),
            UIFactory.Button, UIFactory.ButtonHover, TogglePause, 20);
        UIFactory.CreateButton(parent, "Btn_Restart", "CHƠI LẠI  (R)", new Vector2(1, 1), new Vector2(-296, -30), new Vector2(220, 64),
            UIFactory.Button, UIFactory.ButtonHover, RestartLevel, 20);

        // Thanh gợi ý phím (đáy màn hình)
        RectTransform hint = UIFactory.CreateRect("HUD_Hint", parent, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1180, 52));
        UIFactory.AddImage(hint, new Color(UIFactory.Panel.r, UIFactory.Panel.g, UIFactory.Panel.b, 0.75f));
        hintText = UIFactory.CreateText(hint, HintWhole, 19, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    private void BuildPauseModal(Transform parent)
    {
        RectTransform root = UIFactory.CreateFill("Modal_Pause", parent);
        UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.72f));
        pauseModalRoot = root.gameObject;

        pauseDialogRect = UIFactory.CreateRect("PauseCard", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 520));
        UIFactory.AddImage(pauseDialogRect, UIFactory.Accent);
        RectTransform inner = UIFactory.CreateFill("Inner", pauseDialogRect, 4f);
        UIFactory.AddImage(inner, new Color(0.08f, 0.1f, 0.17f, 1f));

        RectTransform titleRect = UIFactory.CreateRect("Title", inner, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(440, 56));
        UIFactory.CreateText(titleRect, "TẠM DỪNG", 40, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

        RectTransform subRect = UIFactory.CreateRect("Subtitle", inner, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(440, 36));
        pauseSubtitleText = UIFactory.CreateText(subRect, "", 22, UIFactory.TextDim);

        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = new Vector2(340, 62);
        UIFactory.CreateButton(inner, "Btn_Resume", "TIẾP TỤC", center, new Vector2(0, 60), size,
            UIFactory.Accent, UIFactory.AccentHover, ResumeGame);
        UIFactory.CreateButton(inner, "Btn_Restart", "CHƠI LẠI", center, new Vector2(0, -15), size,
            UIFactory.Button, UIFactory.ButtonHover, RestartLevel);
        UIFactory.CreateButton(inner, "Btn_Menu", "CHỌN MÀN", center, new Vector2(0, -90), size,
            UIFactory.Button, UIFactory.ButtonHover, GoToMenu);
        UIFactory.CreateButton(inner, "Btn_Quit", "THOÁT GAME", center, new Vector2(0, -165), size,
            UIFactory.Danger, UIFactory.DangerHover, Application.Quit);

        pauseModalRoot.SetActive(false);
    }

    #endregion

    #region Điều khiển

    private void RestartLevel()
    {
        ResumeGame();
        if (GameManager.Instance != null) GameManager.Instance.RestartLevel();
    }

    private void GoToMenu()
    {
        ResumeGame();
        if (SceneFader.Instance != null) SceneFader.Instance.FadeToScene(LevelProgress.MenuScene, 0.4f);
        else SceneManager.LoadScene(LevelProgress.MenuScene);
    }

    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        pauseModalRoot.SetActive(true);
        StartCoroutine(AnimatePopIn(pauseDialogRect));
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseModalRoot != null) pauseModalRoot.SetActive(false);
    }

    public void HideAllModals()
    {
        ResumeGame();
    }

    /// <summary>
    /// Hiệu ứng nảy khi mở hộp thoại (dùng unscaledDeltaTime để chạy cả khi Time.timeScale = 0)
    /// </summary>
    private IEnumerator AnimatePopIn(RectTransform target)
    {
        const float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float bounce = 1f + 0.35f * Mathf.Sin(t * Mathf.PI) * (1f - t);
            target.localScale = Vector3.one * Mathf.Min(Mathf.Lerp(0.7f, 1f, t) * bounce, 1.08f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    #endregion
}
