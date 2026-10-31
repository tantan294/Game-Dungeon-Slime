using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cửa chặn (Plate Door): là tường (Layer Obstacle) cho tới khi mọi nút nhấn cùng kênh được nhấn cùng lúc.
/// Đã mở thì giữ nguyên trạng thái mở cho tới khi Restart màn chơi.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlateDoor : MonoBehaviour
{
    private static readonly List<PlateDoor> All = new List<PlateDoor>();

    [Tooltip("Kênh liên kết với nút nhấn (PressurePlate) cùng số")]
    [SerializeField] private int channel = 1;

    [Tooltip("Độ trong suốt của cửa sau khi mở")]
    [SerializeField] private float openAlpha = 0.15f;

    private Collider2D doorCollider;
    private SpriteRenderer spriteRenderer;
    private Color closedColor;
    private bool isOpen;

    private void Awake()
    {
        doorCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) closedColor = spriteRenderer.color;
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // Chạy sau Update của PressurePlate để đọc trạng thái nút của frame hiện tại
    private void LateUpdate()
    {
        if (!isOpen && PressurePlate.AllPressed(channel))
        {
            SetOpen(true);
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (doorCollider != null) doorCollider.enabled = !open;
        if (spriteRenderer != null)
        {
            Color c = closedColor;
            if (open) c.a = openAlpha;
            spriteRenderer.color = c;
        }
    }

    /// <summary>
    /// Đóng lại toàn bộ cửa (dùng khi Restart màn chơi)
    /// </summary>
    public static void CloseAll()
    {
        foreach (var door in All) door.SetOpen(false);
    }
}
