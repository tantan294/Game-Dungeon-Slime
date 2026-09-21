using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý Cửa thoát hiểm (Level Exit):
/// Cho phép chọn chính xác Scene tiếp theo bằng cách kéo thả file Scene hoặc gõ tên Scene tùy ý.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    [Header("Chọn Scene tiếp theo")]
#if UNITY_EDITOR
    [Tooltip("Kéo thả trực tiếp file Scene (ví dụ: Level 2) từ cửa sổ Project vào đây!")]
    [SerializeField] private SceneAsset nextSceneAsset;
#endif

    [Tooltip("Tên của Scene tiếp theo. Sẽ tự động cập nhật khi bạn kéo file Scene vào ô trên, hoặc bạn có thể tự gõ tên Scene tùy ý.")]
    [SerializeField] private string nextSceneName = "";

    private bool isCleared = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Tự động lấy tên Scene khi kéo thả file SceneAsset vào Inspector
        if (nextSceneAsset != null)
        {
            nextSceneName = nextSceneAsset.name;
        }
    }
#endif

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCleared) return;

        SlimeMovement slime = other.GetComponent<SlimeMovement>();
        if (slime != null)
        {
            isCleared = true;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCompleted(nextSceneName);
            }
        }
    }
}

