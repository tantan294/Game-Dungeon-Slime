using UnityEngine;

public enum SlimeShape
{
    Square_4x4,       // Khối vuông chuẩn (4 x 4)
    Tall_2x6,         // Dài dọc vừa (2 x 6)
    TallSlim_1x8,     // Siêu dài dọc (1 x 8)
    Flat_6x2,         // Dẹp ngang vừa (6 x 2)
    FlatSlim_8x1      // Siêu dẹp ngang (8 x 1)
}

/// <summary>
/// Quản lý cơ chế biến hình của Slime theo 5 trạng thái:
/// - 4x4 (Gốc)
/// - Trục ngang: 4x4 -> 2x6 <-> 1x8 (khi đâm Trái/Phải dao động giữa 2x6 và 1x8)
/// - Trục dọc:   4x4 -> 6x2 <-> 8x1 (khi đâm Lên/Xuống dao động giữa 6x2 và 8x1)
/// - Khi đâm theo trục ngược lại -> Trở về 4x4
/// Tự động chống lún góc tường 100% khi dãn dài (1x8 và 8x1).
/// </summary>
public class SlimeShapeShifter : MonoBehaviour
{
    [Header("Hình dạng hiện tại")]
    [SerializeField] private SlimeShape currentShape = SlimeShape.Square_4x4;

    [Header("Tùy chọn Layer")]
    [Tooltip("Layer vật cản để tự động đẩy Slime an toàn tránh lún tường khi phình to kích thước")]
    [SerializeField] private LayerMask obstacleLayer;

    private SlimeMovement slimeMovement;
    private BoxCollider2D boxCollider;

    public SlimeShape CurrentShape => currentShape;

    private void Awake()
    {
        slimeMovement = GetComponent<SlimeMovement>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Tự động đồng bộ obstacleLayer từ SlimeMovement nếu chưa chọn ở Inspector
        if (obstacleLayer == 0 && slimeMovement != null)
        {
            obstacleLayer = slimeMovement.ObstacleLayer;
        }

        // Cố định BoxCollider2D size = 1x1 để collider tự động khớp theo transform.localScale
        if (boxCollider != null)
        {
            boxCollider.size = Vector2.one;
        }
    }

    private void OnEnable()
    {
        if (slimeMovement != null)
        {
            slimeMovement.OnHitWall += HandleHitWall;
        }
    }

    private void OnDisable()
    {
        if (slimeMovement != null)
        {
            slimeMovement.OnHitWall -= HandleHitWall;
        }
    }

    private void Start()
    {
        if (obstacleLayer == 0 && slimeMovement != null)
        {
            obstacleLayer = slimeMovement.ObstacleLayer;
        }

        ApplyShape(currentShape, Vector2.zero);
    }

    /// <summary>
    /// Xử lý biến đổi hình dạng theo logic va đập vật lý:
    /// </summary>
    private void HandleHitWall(Vector2 hitDirection)
    {
        bool isHorizontal = Mathf.Abs(hitDirection.x) > 0.5f; // Đâm Trái hoặc Phải
        bool isVertical = Mathf.Abs(hitDirection.y) > 0.5f;   // Đâm Lên hoặc Xuống

        SlimeShape nextShape = currentShape;

        switch (currentShape)
        {
            case SlimeShape.Square_4x4:
                if (isHorizontal)
                {
                    // Ép ngang: 4x4 -> 2x6
                    nextShape = SlimeShape.Tall_2x6;
                }
                else if (isVertical)
                {
                    // Ép dọc: 4x4 -> 6x2
                    nextShape = SlimeShape.Flat_6x2;
                }
                break;

            // Nhóm dài dọc (Tall)
            case SlimeShape.Tall_2x6:
                if (isHorizontal)
                {
                    // Đâm Trái/Phải tiếp -> Thành 1x8
                    nextShape = SlimeShape.TallSlim_1x8;
                }
                else if (isVertical)
                {
                    // Đâm Lên/Xuống -> Nén về 4x4
                    nextShape = SlimeShape.Square_4x4;
                }
                break;

            case SlimeShape.TallSlim_1x8:
                if (isHorizontal)
                {
                    // 1x8 đâm Trái/Phải tiếp -> Lại về 2x6
                    nextShape = SlimeShape.TallSlim_1x8;
                }
                else if (isVertical)
                {
                    // Đâm Lên/Xuống -> Nén về 4x4
                    nextShape = SlimeShape.Tall_2x6;
                }
                break;

            // Nhóm dẹp ngang (Flat)
            case SlimeShape.Flat_6x2:
                if (isVertical)
                {
                    // Đâm Lên/Xuống tiếp -> Thành 8x1
                    nextShape = SlimeShape.FlatSlim_8x1;
                }
                else if (isHorizontal)
                {
                    // Đâm Trái/Phải -> Nén về 4x4
                    nextShape = SlimeShape.Square_4x4;
                }
                break;

            case SlimeShape.FlatSlim_8x1:
                if (isVertical)
                {
                    // 8x1 đâm Lên/Xuống tiếp -> Lại về 6x2
                    nextShape = SlimeShape.FlatSlim_8x1;
                }
                else if (isHorizontal)
                {
                    // Đâm Trái/Phải -> Nén về 4x4
                    nextShape = SlimeShape.Flat_6x2;
                }
                break;
        }

        // Áp dụng nếu có sự thay đổi hình dạng
        if (nextShape != currentShape)
        {
            SlimeShape prevShape = currentShape;
            ApplyShape(nextShape, hitDirection);
            Vector2 size = GetDimensions(nextShape);
            Debug.Log($"[Slime] Đâm tường {(isHorizontal ? "Trái/Phải" : "Lên/Xuống")} -> Biến đổi từ {prevShape} thành {nextShape} (Kích thước: {size.x} x {size.y})");
        }
    }

    /// <summary>
    /// Lấy kích thước (Rộng, Cao) tương ứng cho từng hình dạng
    /// </summary>
    public static Vector2 GetDimensions(SlimeShape shape)
    {
        return shape switch
        {
            SlimeShape.Square_4x4 => new Vector2(1f, 1f),
            SlimeShape.Tall_2x6 => new Vector2(0.5f, 1.5f),
            SlimeShape.TallSlim_1x8 => new Vector2(0.25f, 2f),
            SlimeShape.Flat_6x2 => new Vector2(1.5f, 0.5f),
            SlimeShape.FlatSlim_8x1 => new Vector2(2f, 0.25f),
            _ => new Vector2(4f, 4f)
        };
    }

    /// <summary>
    /// Cập nhật kích thước và căn chỉnh vị trí an toàn để không bao giờ bị lún/tràn ra ngoài tường
    /// </summary>
    public void ApplyShape(SlimeShape newShape, Vector2 hitDirection)
    {
        Vector2 oldSize = GetDimensions(currentShape);
        Vector2 newSize = GetDimensions(newShape);
        currentShape = newShape;

        // 1. Cập nhật scale hiển thị (BoxCollider2D tự động co dãn theo)
        transform.localScale = new Vector3(newSize.x, newSize.y, 1f);

        // 2. Căn chỉnh vị trí mép tiếp xúc với bức tường vừa đâm:
        if (hitDirection != Vector2.zero)
        {
            // Trục ngang: Nếu đâm Trái hoặc Phải
            if (Mathf.Abs(hitDirection.x) > 0.5f)
            {
                float deltaWidth = newSize.x - oldSize.x;
                transform.position -= new Vector3(hitDirection.x * (deltaWidth * 0.5f), 0f, 0f);
            }

            // Trục dọc: Nếu đâm Lên hoặc Xuống
            if (Mathf.Abs(hitDirection.y) > 0.5f)
            {
                float deltaHeight = newSize.y - oldSize.y;
                transform.position -= new Vector3(0f, hitDirection.y * (deltaHeight * 0.5f), 0f);
            }
        }

        // 3. Xử lý triệt để góc tường: Tránh việc trục vuông góc bị dãn dài đâm xuyên vào tường kề bên!
        ClampToRoomBoundaries(newSize, hitDirection);
    }

    /// <summary>
    /// Thuật toán xử lý góc tường chính xác tuyệt đối:
    /// - Khi đâm Trái/Phải: Trục dọc (Y) dãn dài -> Bỏ qua bức tường vừa đâm, chỉ kiểm tra Trần (ở trên) và Sàn (ở dưới) để chống xuyên tường.
    /// - Khi đâm Lên/Xuống: Trục ngang (X) dãn rộng -> Bỏ qua trần/sàn vừa đâm, chỉ kiểm tra Tường Trái/Phải để chống xuyên tường.
    /// </summary>
    private void ClampToRoomBoundaries(Vector2 size, Vector2 hitDirection)
    {
        if (obstacleLayer == 0)
        {
            if (slimeMovement != null) obstacleLayer = slimeMovement.ObstacleLayer;
            if (obstacleLayer == 0) return;
        }

        Vector3 pos = transform.position;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // Quét tất cả vật cản trong phạm vi lân cận
        Collider2D[] walls = Physics2D.OverlapBoxAll(pos, size + Vector2.one * 4f, 0f, obstacleLayer);

        foreach (var col in walls)
        {
            if (col == null || col.gameObject == gameObject) continue;
            Bounds b = col.bounds;

            // TRƯỜNG HỢP 1: Đâm theo trục NGANG (Trái hoặc Phải)
            // Chiều cao (Y) bị dãn -> Chỉ kiểm tra Trần và Sàn, bỏ qua tường vừa đâm!
            if (Mathf.Abs(hitDirection.x) > 0.5f)
            {
                // Bỏ qua bức tường Slime vừa đâm trúng
                if (hitDirection.x > 0.5f && b.min.x >= pos.x + halfW - 0.15f) continue;
                if (hitDirection.x < -0.5f && b.max.x <= pos.x - halfW + 0.15f) continue;

                // Slime có đang nằm trong phạm vi chiều ngang của vật cản này không?
                bool inXRange = (pos.x + halfW > b.min.x + 0.05f) && (pos.x - halfW < b.max.x - 0.05f);
                if (inXRange)
                {
                    // Trần (vật cản nằm ở phía trên Slime: đáy của trần b.min.y phải cao hơn tâm Slime)
                    if (b.min.y >= pos.y && pos.y + halfH > b.min.y)
                    {
                        pos.y = b.min.y - halfH; // Đẩy tụt xuống để mép trên chạm khít mép dưới của trần
                    }
                    // Sàn (vật cản nằm ở phía dưới Slime: đỉnh của sàn b.max.y phải thấp hơn tâm Slime)
                    else if (b.max.y <= pos.y && pos.y - halfH < b.max.y)
                    {
                        pos.y = b.max.y + halfH; // Đẩy vọt lên để mép dưới chạm khít mép trên của sàn
                    }
                }
            }
            // TRƯỜNG HỢP 2: Đâm theo trục DỌC (Lên hoặc Xuống)
            // Chiều ngang (X) bị dãn -> Chỉ kiểm tra Tường Trái và Tường Phải, bỏ qua trần/sàn vừa đâm!
            else if (Mathf.Abs(hitDirection.y) > 0.5f)
            {
                // Bỏ qua trần hoặc sàn Slime vừa đâm trúng
                if (hitDirection.y > 0.5f && b.min.y >= pos.y + halfH - 0.15f) continue;
                if (hitDirection.y < -0.5f && b.max.y <= pos.y - halfH + 0.15f) continue;

                // Slime có đang nằm trong phạm vi chiều dọc của vật cản này không?
                bool inYRange = (pos.y + halfH > b.min.y + 0.05f) && (pos.y - halfH < b.max.y - 0.05f);
                if (inYRange)
                {
                    // Tường Phải (vật cản nằm bên phải: mép trái của tường b.min.x phải nằm bên phải tâm Slime)
                    if (b.min.x >= pos.x && pos.x + halfW > b.min.x)
                    {
                        pos.x = b.min.x - halfW; // Đẩy sang trái để mép phải chạm khít mép trái của tường
                    }
                    // Tường Trái (vật cản nằm bên trái: mép phải của tường b.max.x phải nằm bên trái tâm Slime)
                    else if (b.max.x <= pos.x && pos.x - halfW < b.max.x)
                    {
                        pos.x = b.max.x + halfW; // Đẩy sang phải để mép trái chạm khít mép phải của tường
                    }
                }
            }
        }

        transform.position = pos;
        Physics2D.SyncTransforms();
    }
}
