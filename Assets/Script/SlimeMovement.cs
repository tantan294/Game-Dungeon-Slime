using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quản lý di chuyển trượt theo dạng lưới của Slime.
/// Slime sẽ trượt theo một hướng cho tới khi chạm vào vật cản (Obstacle).
/// Xử lý chống kẹt góc 100% bằng cách chỉ nhận diện tường đối diện phía trước.
/// </summary>
public class SlimeMovement : MonoBehaviour
{
    [Header("Cấu hình di chuyển")]
    [Tooltip("Tốc độ trượt (đơn vị/giây)")]
    [SerializeField] private float slideSpeed = 12f;

    [Tooltip("Layer chứa các vật cản (Tường, Khối đá)")]
    [SerializeField] private LayerMask obstacleLayer;

    // Trạng thái nội bộ
    private bool isSliding = false;
    private BoxCollider2D boxCollider;

    // Sự kiện khi va chạm vào tường sau khi trượt xong (để kích hoạt đổi hình dạng)
    public System.Action<Vector2> OnHitWall;

    public bool IsSliding => isSliding;
    public LayerMask ObstacleLayer => obstacleLayer;

    private Rigidbody2D rb;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        // Đảm bảo Slime có Rigidbody2D dạng Kinematic để Unity kích hoạt OnTriggerEnter2D khi chạm Gai
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        // Cảnh báo nếu vô tình gán chính Slime vào Layer Obstacle
        if (((1 << gameObject.layer) & obstacleLayer) != 0)
        {
            Debug.LogError("[LỖI CẤU HÌNH] Slime đang ở Layer Obstacle! Hãy đổi Layer của Slime về 'Default'. Layer 'Obstacle' chỉ dành riêng cho Tường.");
        }
    }

    private void Update()
    {
        // Khi đang trượt, không nhận thêm lệnh di chuyển mới
        if (isSliding) return;

        Vector2 inputDir = GetInputDirection();
        if (inputDir != Vector2.zero)
        {
            TrySlide(inputDir);
        }
    }

    /// <summary>
    /// Đọc input từ bàn phím (hỗ trợ cả Input System mới và cũ của Unity)
    /// </summary>
    private Vector2 GetInputDirection()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return Vector2.up;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return Vector2.down;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return Vector2.left;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return Vector2.right;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return Vector2.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return Vector2.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return Vector2.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return Vector2.right;
#endif

        return Vector2.zero;
    }

    /// <summary>
    /// Kiểm tra đường đi và bắt đầu trượt nếu có khoảng trống
    /// </summary>
    private void TrySlide(Vector2 direction)
    {
        // Lấy kích thước thực tế trong thế giới của Slime
        Vector2 slimeSize = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 castBoxSize = slimeSize;

        // Kỹ thuật Skin Width: Thu nhỏ nhẹ 0.1 ở trục vuông góc để không cọ xát tường song song
        if (Mathf.Abs(direction.x) > 0.5f)
        {
            castBoxSize.y = Mathf.Max(0.1f, slimeSize.y - 0.1f);
        }
        else
        {
            castBoxSize.x = Mathf.Max(0.1f, slimeSize.x - 0.1f);
        }

        // Tạm tắt collider để tia quét không tự quét trúng Slime
        bool colEnabled = boxCollider != null && boxCollider.enabled;
        if (colEnabled) boxCollider.enabled = false;

        // Quét TẤT CẢ các vật thể trên đường đi để lọc chính xác bức tường phía trước
        RaycastHit2D[] hits = Physics2D.BoxCastAll(transform.position, castBoxSize, 0f, direction, 100f, obstacleLayer);

        if (colEnabled) boxCollider.enabled = true;

        RaycastHit2D bestHit = default;
        float minDistance = float.MaxValue;
        bool foundWallInFront = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.gameObject == gameObject) continue;

            // ĐIỀU KIỆN CHỐNG KẸT GÓC:
            // 1. Mặt của bức tường phải hướng NGƯỢC CHIỀU di chuyển (Dot < -0.3).
            //    -> Giúp loại bỏ hoàn toàn bức tường nằm sau lưng hoặc tường bên sườn khi đứng ở góc!
            float normalDot = Vector2.Dot(h.normal, direction);
            if (normalDot > -0.3f) continue;

            // 2. Điểm tiếp xúc phải nằm phía trước tâm Slime (không nằm sau lưng)
            Vector2 toHitPoint = (Vector2)h.point - (Vector2)transform.position;
            if (Vector2.Dot(toHitPoint, direction) < -0.05f) continue;

            // Chọn bức tường gần nhất chắn trước mặt
            if (h.distance < minDistance)
            {
                minDistance = h.distance;
                bestHit = h;
                foundWallInFront = true;
            }
        }

        if (foundWallInFront)
        {
            // Nếu đang đứng sát bức tường phía trước (< 0.05) thì không thể đi tiếp hướng này
            if (bestHit.distance < 0.05f)
            {
                Debug.Log($"[Slime] Đang áp sát tường phía trước ({direction}), hãy chọn hướng khác!");
                return;
            }

            // Trượt chính xác tới mép tường phía trước
            Vector2 targetPos = (Vector2)transform.position + direction * bestHit.distance;
            StartCoroutine(SlideRoutine(targetPos, direction));
        }
        else
        {
            // NẾU KHÔNG CÓ TƯỜNG CHẶN: Kiểm tra xem phía trước có Cửa Thoát Hiểm (Exit) không!
            RaycastHit2D[] allHits = Physics2D.BoxCastAll(transform.position, castBoxSize, 0f, direction, 100f);
            foreach (var h in allHits)
            {
                if (h.collider != null && h.collider.GetComponent<LevelExit>() != null)
                {
                    // Tìm thấy Exit hợp lệ phía trước! Trượt thẳng vào cửa Exit để qua màn
                    Vector2 targetPos = h.collider.transform.position;
                    StartCoroutine(SlideRoutine(targetPos, direction));
                    return;
                }
            }

            Debug.LogWarning($"[Slime] Hướng {direction} không có bức tường nào chặn lại! Cần dựng tường bao quanh phòng hoặc đặt Cửa Exit.");
        }
    }

    /// <summary>
    /// Coroutine di chuyển Slime mượt mà từ vị trí hiện tại tới điểm đích
    /// </summary>
    private IEnumerator SlideRoutine(Vector2 targetPos, Vector2 direction)
    {
        isSliding = true;

        while (Vector2.Distance(transform.position, targetPos) > 0.001f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPos,
                slideSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Đặt chính xác vị trí đích chạm khít mép tường
        transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        isSliding = false;

        // Kích hoạt sự kiện đâm vào tường để đổi hình dạng
        OnHitWall?.Invoke(direction);
    }

    /// <summary>
    /// Đưa Slime về vị trí chỉ định và hủy trạng thái đang trượt (dùng khi Restart/Respawn)
    /// </summary>
    public void ResetToPosition(Vector3 newPosition)
    {
        StopAllCoroutines();
        transform.position = newPosition;
        isSliding = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<HazardSpike>() != null)
        {
            GameManager.Instance.OnSlimeDied();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 size = new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 1f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
