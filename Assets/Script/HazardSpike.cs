using UnityEngine;

/// <summary>
/// Quản lý Bẫy gai nhọn (Spike Hazard):
/// Khi Slime trượt chạm vào bẫy gai -> Kích hoạt Slime chết và gọi GameManager hồi sinh.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardSpike : MonoBehaviour
{
    private void Awake()
    {
        // Đảm bảo Collider luôn là Trigger để Slime có thể lướt vào và kích hoạt sự kiện chết
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Slime không
        SlimeMovement slime = other.GetComponent<SlimeMovement>();
        if (slime != null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSlimeDied();
            }
        }
    }
}

