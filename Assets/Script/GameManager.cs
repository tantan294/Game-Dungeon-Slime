using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quản lý vòng lặp trò chơi (Game Loop):
/// - Tự động tạo nếu trong Scene chưa có GameObject GameManager.
/// - Lắng nghe phím 'R' để Restart màn chơi ngay lập tức.
/// - Xử lý khi Slime chết (chạm bẫy gai) và tự động hồi sinh sau 0.5s.
/// - Xử lý khi hoàn thành màn chơi (chạm Cửa Exit).
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager (AutoCreated)");
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Tham chiếu Slime")]
    [SerializeField] private SlimeMovement slimeMovement;
    [SerializeField] private SlimeShapeShifter slimeShapeShifter;
    private SlimeSplitter slimeSplitter;

    [Header("Cấu hình hồi sinh")]
    [Tooltip("Thời gian chờ hồi sinh sau khi chết (giây)")]
    [SerializeField] private float respawnDelay = 0.5f;

    private Vector3 spawnPosition;
    private bool isDead = false;
    private SpriteRenderer slimeSpriteRenderer;

    // Tự động khởi tạo GameManager khi bấm Play mà không cần người dùng tự tạo GameObject
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance == null)
        {
            var existing = FindFirstObjectByType<GameManager>();
            if (existing == null)
            {
                GameObject go = new GameObject("GameManager");
                _instance = go.AddComponent<GameManager>();
            }
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitSlimeReferences();

        // Ghi nhớ màn đang chơi để Menu hiển thị
        int level = LevelProgress.GetLevelNumber(scene.name);
        if (level > 0) LevelProgress.LastPlayed = level;
    }

    private void Start()
    {
        InitSlimeReferences();
    }

    private void InitSlimeReferences()
    {
        // Tự động tìm Slime trong Scene nếu chưa được kéo vào
        if (slimeMovement == null)
        {
            slimeMovement = FindFirstObjectByType<SlimeMovement>();
        }

        if (slimeMovement != null)
        {
            slimeShapeShifter = slimeMovement.GetComponent<SlimeShapeShifter>();
            slimeSpriteRenderer = slimeMovement.GetComponent<SpriteRenderer>();
            spawnPosition = slimeMovement.transform.position;

            // Gắn cơ chế Tách đôi (phím F) nếu Slime chưa có
            slimeSplitter = slimeMovement.GetComponent<SlimeSplitter>();
            if (slimeSplitter == null)
            {
                slimeSplitter = slimeMovement.gameObject.AddComponent<SlimeSplitter>();
            }
        }
    }

    private void Update()
    {
        // Nếu chưa tìm thấy Slime thì tìm lại
        if (slimeMovement == null)
        {
            InitSlimeReferences();
        }

        // Nhấn phím 'R' để Restart level tức thì (bỏ qua ở Menu, nơi không có Slime)
        if (slimeMovement != null && IsRestartPressed())
        {
            RestartLevel();
        }
    }

    private bool IsRestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) return true;
#endif

        return false;
    }

    /// <summary>
    /// Chơi lại màn chơi: đưa Slime về vị trí xuất phát và hình dạng 4x4 gốc
    /// </summary>
    public void RestartLevel()
    {
        StopAllCoroutines();
        isDead = false;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideAllModals();
        }

        // Gộp lại nếu đang tách đôi (xóa nửa B, trả Slime chính về kích thước nguyên khối)
        if (slimeSplitter != null)
        {
            slimeSplitter.ResetSplit();
        }

        // Đóng lại các cửa đã mở bằng nút nhấn
        PlateDoor.CloseAll();

        if (slimeMovement != null)
        {
            // Đưa Slime về vị trí ban đầu
            slimeMovement.ResetToPosition(spawnPosition);

            // Bật lại hiển thị và điều khiển
            if (slimeSpriteRenderer != null) slimeSpriteRenderer.enabled = true;
            slimeMovement.enabled = true;

            // Đưa về hình dạng 4x4 chuẩn
            if (slimeShapeShifter != null)
            {
                slimeShapeShifter.ApplyShape(SlimeShape.Square_4x4, Vector2.zero);
            }

            Debug.Log("[GameManager] Đã Restart màn chơi! Slime trở về vị trí xuất phát (4x4).");
        }
    }

    /// <summary>
    /// Xử lý khi Slime chạm vào bẫy gai (chết)
    /// </summary>
    public void OnSlimeDied()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[GameManager] Slime đã chết vì bẫy gai! Đang chuẩn bị hồi sinh sau " + respawnDelay + "s...");

        // Khóa điều khiển và ẩn hình ảnh Slime (cả hai nửa nếu đang tách đôi)
        foreach (var slime in DisableAllSlimes())
        {
            SpriteRenderer sr = slime.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        StartCoroutine(RespawnRoutine());
    }

    /// <summary>
    /// Khóa điều khiển tất cả Slime đang hoạt động, trả về danh sách vừa bị khóa
    /// </summary>
    private SlimeMovement[] DisableAllSlimes()
    {
        SlimeMovement[] slimes = SlimeMovement.ActiveSlimes.ToArray();
        foreach (var slime in slimes)
        {
            slime.enabled = false;
        }
        return slimes;
    }

    /// <summary>
    /// Slime (hoặc một nửa) chạm Cửa Exit. Khi đang tách đôi, nửa đến trước sẽ chui vào cửa chờ nửa còn lại.
    /// Trả về true nếu màn chơi đã hoàn thành.
    /// </summary>
    public bool OnSlimeReachedExit(SlimeMovement slime, string nextSceneName)
    {
        bool otherHalfOutside = SlimeMovement.ActiveSlimes.Exists(s => s != slime);
        if (otherHalfOutside)
        {
            Debug.Log("[GameManager] Một nửa Jelly đã vào cửa! Đưa nửa còn lại tới cửa để qua màn.");
            slime.enabled = false;
            StartCoroutine(ShrinkRoutine(slime));
            return false;
        }

        OnLevelCompleted(nextSceneName, slime);
        return true;
    }

    /// <summary>
    /// Hiệu ứng Slime thu nhỏ dần vào tâm cổng Exit (0.35s) rồi ẩn đi
    /// </summary>
    private IEnumerator ShrinkRoutine(SlimeMovement slime)
    {
        Transform t = slime.transform;
        Vector3 startScale = t.localScale;
        float elapsed = 0f;
        float shrinkDuration = 0.35f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / shrinkDuration);
            yield return null;
        }

        SpriteRenderer sr = slime.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        RestartLevel();
    }

    /// <summary>
    /// Xử lý khi Slime chạm vào Cửa thoát hiểm (Exit): Hiệu ứng thu nhỏ Slime rồi chuyển ngay sang màn tiếp theo
    /// </summary>
    public void OnLevelCompleted(string nextSceneName = "", SlimeMovement finisher = null)
    {
        Debug.Log("[GameManager] Đã vượt qua màn chơi!");
        LevelProgress.MarkCleared(LevelProgress.GetLevelNumber(SceneManager.GetActiveScene().name));

        // Khóa điều khiển Slime
        DisableAllSlimes();
        if (slimeMovement != null) slimeMovement.enabled = false;

        StartCoroutine(LevelCompleteRoutine(nextSceneName, finisher != null ? finisher : slimeMovement));
    }

    private IEnumerator LevelCompleteRoutine(string nextSceneName, SlimeMovement finisher)
    {
        // 1. Hiệu ứng Slime thu nhỏ dần vào tâm cổng Exit (0.35s)
        if (finisher != null)
        {
            yield return ShrinkRoutine(finisher);
        }

        // 2. Chuyển thẳng sang màn tiếp theo (Fade), không hiện Modal Chiến Thắng
        LoadNextScene(nextSceneName);
    }

    /// <summary>
    /// Chuyển sang Scene tiếp theo có hiệu ứng làm mờ màn hình (Fade Out)
    /// </summary>
    public void LoadNextScene(string nextSceneName = "")
    {
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(nextSceneName, 0.4f);
        }
        else
        {
            // Dự phòng chuyển trực tiếp nếu chưa có SceneFader
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
                int nextSceneIndex = currentSceneIndex + 1;

                if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
                {
                    SceneManager.LoadScene(nextSceneIndex);
                }
                else
                {
                    Debug.Log("[GameManager] Đã hoàn thành màn cuối cùng! Quay về Menu.");
                    SceneManager.LoadScene(LevelProgress.MenuScene);
                }
            }
        }
    }
}
