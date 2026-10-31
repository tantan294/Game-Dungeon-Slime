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

    [Header("Sprite Jelly theo từng hình dạng")]
    [Tooltip("Để trống sẽ tự động nạp từ Resources/Jelly")]
    [SerializeField] private Sprite spriteSquare;
    [SerializeField] private Sprite spriteTall;
    [SerializeField] private Sprite spriteTallSlim;
    [SerializeField] private Sprite spriteFlat;
    [SerializeField] private Sprite spriteFlatSlim;

    private SlimeMovement slimeMovement;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;

    // Hệ số kích thước: 1 = nguyên khối, 0.5 = một nửa sau khi tách đôi
    private float sizeMultiplier = 1f;

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

        InitShapeSprites();
    }

    /// <summary>
    /// Nạp sprite Jelly còn thiếu từ Resources và cấu hình SpriteRenderer:
    /// Sliced + size 1x1 để sprite luôn lấp đầy đúng 1x1 trước khi nhân với localScale,
    /// nhờ đó mỗi sprite (đã vẽ đúng tỉ lệ hình dạng) hiển thị không bị méo pixel.
    /// </summary>
    private void InitShapeSprites()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (spriteSquare == null) spriteSquare = Resources.Load<Sprite>("Jelly/Jelly_Square_4x4");
        if (spriteTall == null) spriteTall = Resources.Load<Sprite>("Jelly/Jelly_Tall_2x6");
        if (spriteTallSlim == null) spriteTallSlim = Resources.Load<Sprite>("Jelly/Jelly_TallSlim_1x8");
        if (spriteFlat == null) spriteFlat = Resources.Load<Sprite>("Jelly/Jelly_Flat_6x2");
        if (spriteFlatSlim == null) spriteFlatSlim = Resources.Load<Sprite>("Jelly/Jelly_FlatSlim_8x1");

        if (spriteSquare != null)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = Vector2.one;
            spriteRenderer.color = Color.white; // Sprite đã có màu sẵn, bỏ màu tint cũ
        }
    }

    private Sprite GetShapeSprite(SlimeShape shape)
    {
        return shape switch
        {
            SlimeShape.Tall_2x6 => spriteTall,
            SlimeShape.TallSlim_1x8 => spriteTallSlim,
            SlimeShape.Flat_6x2 => spriteFlat,
            SlimeShape.FlatSlim_8x1 => spriteFlatSlim,
            _ => spriteSquare
        };
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
            if (!ApplyShape(nextShape, hitDirection))
            {
                Debug.Log($"[Slime] Không đủ chỗ để biến từ {prevShape} thành {nextShape} -> Giữ nguyên hình dạng.");
                return;
            }
            Vector2 size = GetSize(nextShape);
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
    /// Kích thước thực tế của Slime này (đã nhân hệ số tách đôi)
    /// </summary>
    public Vector2 GetSize(SlimeShape shape)
    {
        return GetDimensions(shape) * sizeMultiplier;
    }

    /// <summary>
    /// Đặt ngay hình dạng, hệ số kích thước và vị trí (dùng khi tách đôi / hợp nhất / chơi lại), không kiểm tra va chạm
    /// </summary>
    public void ForceShape(SlimeShape shape, float multiplier, Vector3 position)
    {
        currentShape = shape;
        sizeMultiplier = multiplier;
        SetShapeVisual(shape);
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>
    /// Thử đặt hình dạng tại vị trí chỉ định, hoàn tác và trả về false nếu bị chồng lấn vật cản
    /// </summary>
    public bool TryPlaceShape(SlimeShape shape, float multiplier, Vector3 position)
    {
        SlimeShape oldShape = currentShape;
        float oldMultiplier = sizeMultiplier;
        Vector3 oldPos = transform.position;

        ForceShape(shape, multiplier, position);
        if (IsOverlappingObstacleExact(GetSize(shape)))
        {
            ForceShape(oldShape, oldMultiplier, oldPos);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Cập nhật kích thước và căn chỉnh vị trí an toàn để không bao giờ bị lún/tràn ra ngoài tường.
    /// Trả về false (và giữ nguyên hình dạng cũ) nếu không đủ chỗ để biến hình.
    /// </summary>
    public bool ApplyShape(SlimeShape newShape, Vector2 hitDirection)
    {
        SlimeShape oldShape = currentShape;
        Vector3 oldPos = transform.position;
        Vector2 oldSize = GetSize(currentShape);
        Vector2 newSize = GetSize(newShape);
        currentShape = newShape;

        // 1. Cập nhật scale hiển thị (BoxCollider2D tự động co dãn theo)
        SetShapeVisual(newShape);

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
        ClampToRoomBoundaries(newSize, hitDirection, oldPos, oldSize);

        // 4. Không đủ chỗ (VD: hành lang thấp hơn chiều dài mới) -> Hoàn tác, giữ hình dạng cũ để không bị kẹt trong tường
        if (hitDirection != Vector2.zero && IsOverlappingObstacle(newSize))
        {
            ForceShape(oldShape, sizeMultiplier, oldPos);
            return false;
        }

        return true;
    }

    private void SetShapeVisual(SlimeShape shape)
    {
        Vector2 size = GetSize(shape);
        transform.localScale = new Vector3(size.x, size.y, 1f);

        // Đổi sprite Jelly tương ứng với hình dạng
        Sprite shapeSprite = GetShapeSprite(shape);
        if (spriteRenderer != null && shapeSprite != null)
        {
            spriteRenderer.sprite = shapeSprite;
        }
    }

    /// <summary>
    /// Kiểm tra thân Slime có đang chồng lấn vào vật cản không (bỏ qua tiếp xúc sát mép).
    /// Thu nhỏ 0.1 (giống Skin Width của SlimeMovement) vì collider 2D có viền contact offset ~0.01 mỗi phía,
    /// thu nhỏ ít hơn sẽ coi việc chạm sát tường là chồng lấn.
    /// Khi đã tách đôi, nửa Slime còn lại cũng được tính là vật cản.
    /// </summary>
    private bool IsOverlappingObstacle(Vector2 size)
    {
        Vector2 checkSize = new Vector2(Mathf.Max(0.01f, size.x - 0.1f), Mathf.Max(0.01f, size.y - 0.1f));
        if (OverlapsOtherSlime(checkSize)) return true;
        if (obstacleLayer == 0) return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, checkSize, 0f, obstacleLayer);
        foreach (var col in hits)
        {
            if (col != null && col.gameObject != gameObject) return true;
        }
        return false;
    }

    /// <summary>
    /// Thân có kích thước checkSize tại vị trí hiện tại có chồng lên nửa Slime còn lại không
    /// </summary>
    private bool OverlapsOtherSlime(Vector2 checkSize)
    {
        Rect body = new Rect((Vector2)transform.position - checkSize * 0.5f, checkSize);
        foreach (var other in SlimeMovement.ActiveSlimes)
        {
            if (other != null && other.gameObject != gameObject && body.Overlaps(other.OccupiedRect)) return true;
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra chồng lấn theo đúng biên collider (chỉ dung sai 0.002 cho sai số số thực).
    /// Dùng khi đặt khối gộp: vị trí được tính sẵn nên không được phép lấn vào tường dù chỉ một chút,
    /// nếu không thân Jelly sẽ kẹt trong tường và trượt xuyên qua được.
    /// </summary>
    private bool IsOverlappingObstacleExact(Vector2 size)
    {
        // Nửa Slime còn lại vẫn dùng dung sai 0.1 như khi biến hình; tường thì so đúng biên (bao hàm luôn phép thử thu nhỏ 0.1)
        if (OverlapsOtherSlime(new Vector2(Mathf.Max(0.01f, size.x - 0.1f), Mathf.Max(0.01f, size.y - 0.1f)))) return true;
        if (obstacleLayer == 0) return false;

        const float tolerance = 0.002f;
        Rect body = new Rect((Vector2)transform.position - size * 0.5f + Vector2.one * tolerance,
            size - Vector2.one * (tolerance * 2f));
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, size + Vector2.one * 0.2f, 0f, obstacleLayer);
        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;
            Bounds b = col.bounds;
            if (body.Overlaps(new Rect(b.min, b.size))) return true;
        }
        return false;
    }

    /// <summary>
    /// Thuật toán xử lý góc tường chính xác tuyệt đối:
    /// - Khi đâm Trái/Phải: Trục dọc (Y) dãn dài -> Bỏ qua bức tường vừa đâm, chỉ kiểm tra Trần (ở trên) và Sàn (ở dưới) để chống xuyên tường.
    /// - Khi đâm Lên/Xuống: Trục ngang (X) dãn rộng -> Bỏ qua trần/sàn vừa đâm, chỉ kiểm tra Tường Trái/Phải để chống xuyên tường.
    /// Phạm vi kiểm tra lấy theo cả thân CŨ lẫn thân MỚI: thân cũ rộng hơn đang chạm trần/sàn ở hai bên khe,
    /// nên phần dãn dài không được phép lọt vào khe hẹp nằm ngay trên/dưới.
    /// </summary>
    private void ClampToRoomBoundaries(Vector2 size, Vector2 hitDirection, Vector3 oldPos, Vector2 oldSize)
    {
        if (obstacleLayer == 0)
        {
            if (slimeMovement != null) obstacleLayer = slimeMovement.ObstacleLayer;
            if (obstacleLayer == 0) return;
        }

        Vector3 pos = transform.position;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // Phạm vi bao trùm thân cũ + thân mới
        float spanMinX = Mathf.Min(pos.x - halfW, oldPos.x - oldSize.x * 0.5f);
        float spanMaxX = Mathf.Max(pos.x + halfW, oldPos.x + oldSize.x * 0.5f);
        float spanMinY = Mathf.Min(pos.y - halfH, oldPos.y - oldSize.y * 0.5f);
        float spanMaxY = Mathf.Max(pos.y + halfH, oldPos.y + oldSize.y * 0.5f);

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
                bool inXRange = (spanMaxX > b.min.x + 0.05f) && (spanMinX < b.max.x - 0.05f);
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
                bool inYRange = (spanMaxY > b.min.y + 0.05f) && (spanMinY < b.max.y - 0.05f);
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
