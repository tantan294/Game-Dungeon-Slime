using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiệu ứng chuyển cảnh (Fade Transition):
/// - Tự động tạo Canvas và màn đen che phủ (không cần thiết lập thủ công).
/// - Tồn tại xuyên suốt các màn chơi (DontDestroyOnLoad).
/// - Mờ dần sang đen khi thoát màn (Fade Out) và sáng dần lên khi vào màn mới (Fade In).
/// </summary>
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    private CanvasGroup canvasGroup;
    private Image blackImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance == null)
        {
            GameObject faderObj = new GameObject("[SceneFader]");
            DontDestroyOnLoad(faderObj);
            Instance = faderObj.AddComponent<SceneFader>();
            Instance.SetupUI(faderObj);
        }
    }

    private void SetupUI(GameObject root)
    {
        // 1. Tạo Canvas hiển thị trên cùng (Overlay)
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Đảm bảo luôn nằm trên cùng màn hình

        root.AddComponent<CanvasScaler>();
        canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f; // Bắt đầu ở màn đen để fade in mượt mà
        canvasGroup.blocksRaycasts = false;

        // 2. Tạo hình ảnh màu đen bao phủ toàn màn hình
        GameObject imgObj = new GameObject("BlackOverlay");
        imgObj.transform.SetParent(root.transform, false);

        blackImage = imgObj.AddComponent<Image>();
        blackImage.color = Color.black;

        RectTransform rect = blackImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
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
        // Khi game vừa khởi động, tự động mờ dần từ đen sang sáng (Fade In)
        StartCoroutine(FadeRoutine(1f, 0f, 0.5f));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Mỗi khi load xong một Scene mới, tự động sáng dần lên (Fade In)
        StartCoroutine(FadeRoutine(1f, 0f, 0.5f));
    }

    /// <summary>
    /// Kích hoạt hiệu ứng tối dần màn hình và tải Scene tiếp theo
    /// </summary>
    public void FadeToScene(string sceneName, float duration = 0.5f)
    {
        StartCoroutine(FadeAndLoadRoutine(sceneName, duration));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName, float duration)
    {
        // 1. Màn hình tối dần sang đen (Fade Out)
        yield return StartCoroutine(FadeRoutine(0f, 1f, duration));

        // 2. Tải Scene tiếp theo
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            int currentIdx = SceneManager.GetActiveScene().buildIndex;
            int nextIdx = currentIdx + 1;

            if (nextIdx < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(nextIdx);
            }
            else
            {
                Debug.Log("[SceneFader] Đã hết các màn chơi! Quay về màn đầu tiên.");
                SceneManager.LoadScene(0);
            }
        }
    }

    /// <summary>
    /// Coroutine nội suy alpha của màn đen từ startAlpha sang endAlpha
    /// </summary>
    public IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.blocksRaycasts = (endAlpha > 0.5f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        canvasGroup.blocksRaycasts = (endAlpha > 0.5f);
    }
}

