using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cơ chế Tách đôi / Hợp nhất của Jelly (gắn trên Slime chính, GameManager tự thêm nếu thiếu):
/// - Nhấn 'F' khi còn nguyên khối: tách thành 2 nửa (mỗi nửa bằng 1/2 kích thước, giữ nguyên hình dạng).
///   Nửa A (Slime chính) đi bằng WASD, nửa B (bản sao, ánh tím) đi bằng phím mũi tên.
/// - Nhấn 'F' khi hai nửa đang chạm nhau: hợp nhất lại thành một khối.
/// </summary>
public class SlimeSplitter : MonoBehaviour
{
    private const float HalfMultiplier = 0.5f;

    [Tooltip("Màu tint của nửa B (điều khiển bằng phím mũi tên) để phân biệt với nửa A")]
    [SerializeField] private Color halfTint = new Color(0.75f, 0.8f, 1f, 1f);

    private SlimeMovement mainMovement;
    private SlimeShapeShifter mainShifter;
    private SlimeMovement halfMovement;

    public bool IsSplit => halfMovement != null;

    private void Awake()
    {
        mainMovement = GetComponent<SlimeMovement>();
        mainShifter = GetComponent<SlimeShapeShifter>();
    }

    private void Update()
    {
        if (!IsSplitPressed()) return;

        if (IsSplit) TryMerge();
        else TrySplit();
    }

    private bool IsSplitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F)) return true;
#endif

        return false;
    }

    /// <summary>
    /// Tách Slime thành 2 nửa nằm cạnh nhau theo chiều dài của hình dạng hiện tại (vẫn nằm gọn trong thân cũ)
    /// </summary>
    private void TrySplit()
    {
        if (mainMovement == null || mainShifter == null) return;
        if (!mainMovement.enabled || mainMovement.IsSliding) return;

        SlimeShape shape = mainShifter.CurrentShape;
        Vector2 size = mainShifter.GetSize(shape);
        Vector3 pos = transform.position;

        // Nửa A: bên trái (hình ngang/vuông) hoặc bên trên (hình dọc); nửa B ở phía đối diện
        Vector3 offset = size.x >= size.y
            ? new Vector3(-size.x * 0.25f, 0f, 0f)
            : new Vector3(0f, size.y * 0.25f, 0f);

        GameObject clone = Instantiate(gameObject, pos, transform.rotation, transform.parent);
        clone.name = gameObject.name + " (Half)";
        DestroyImmediate(clone.GetComponent<SlimeSplitter>());

        halfMovement = clone.GetComponent<SlimeMovement>();
        SlimeShapeShifter halfShifter = clone.GetComponent<SlimeShapeShifter>();

        mainShifter.ForceShape(shape, HalfMultiplier, pos + offset);
        halfShifter.ForceShape(shape, HalfMultiplier, pos - offset);

        mainMovement.ControlScheme = SlimeControlScheme.WASD;
        halfMovement.ControlScheme = SlimeControlScheme.Arrows;

        SpriteRenderer halfRenderer = clone.GetComponent<SpriteRenderer>();
        if (halfRenderer != null) halfRenderer.color = halfTint;

        Debug.Log("[Slime] Tách đôi! Nửa A: WASD | Nửa B: Phím mũi tên. Nhấn F khi hai nửa chạm nhau để hợp nhất.");
    }

    /// <summary>
    /// Hợp nhất hai nửa (phải đang chạm nhau) thành một khối tại tâm của hai nửa
    /// </summary>
    private void TryMerge()
    {
        if (!mainMovement.enabled || !halfMovement.enabled) return;
        if (mainMovement.IsSliding || halfMovement.IsSliding) return;

        Rect a = mainMovement.OccupiedRect;
        Rect b = halfMovement.OccupiedRect;

        // Cho phép hở tối đa 0.05 giữa hai nửa
        Rect aTouch = new Rect(a.xMin - 0.05f, a.yMin - 0.05f, a.width + 0.1f, a.height + 0.1f);
        if (!aTouch.Overlaps(b))
        {
            Debug.Log("[Slime] Hai nửa phải chạm nhau mới hợp nhất được!");
            return;
        }

        float z = transform.position.z;
        Vector2 unionMin = Vector2.Min(a.min, b.min);
        Vector2 unionMax = Vector2.Max(a.max, b.max);
        Vector2 unionCenter = (unionMin + unionMax) * 0.5f;
        SlimeShape[] shapes =
        {
            mainShifter.CurrentShape,
            SlimeShape.Square_4x4,
            SlimeShape.Tall_2x6,
            SlimeShape.Flat_6x2,
            SlimeShape.TallSlim_1x8,
            SlimeShape.FlatSlim_8x1
        };

        // Ẩn nửa B trước để nó không chiếm chỗ khi đặt lại khối gộp
        GameObject half = halfMovement.gameObject;
        half.SetActive(false);

        // Ưu tiên giữ hình dạng hiện tại; với mỗi hình thử đặt ở giữa, rồi áp mép theo vùng bao của hai nửa (tránh lún tường)
        foreach (var s in shapes)
        {
            Vector2 size = SlimeShapeShifter.GetDimensions(s);
            float[] xs = { unionCenter.x, unionMin.x + size.x * 0.5f, unionMax.x - size.x * 0.5f };
            float[] ys = { unionCenter.y, unionMin.y + size.y * 0.5f, unionMax.y - size.y * 0.5f };

            foreach (var x in xs)
            {
                foreach (var y in ys)
                {
                    if (mainShifter.TryPlaceShape(s, 1f, new Vector3(x, y, z)))
                    {
                        ClearHalf();
                        Debug.Log($"[Slime] Hợp nhất thành công -> {s}");
                        return;
                    }
                }
            }
        }

        half.SetActive(true);
        Debug.Log("[Slime] Không đủ chỗ để hợp nhất ở đây!");
    }

    /// <summary>
    /// Hủy trạng thái tách đôi ngay lập tức (dùng khi chơi lại màn)
    /// </summary>
    public void ResetSplit()
    {
        ClearHalf();
        if (mainShifter != null)
        {
            mainShifter.ForceShape(mainShifter.CurrentShape, 1f, transform.position);
        }
    }

    /// <summary>
    /// Xóa nửa B (tắt ngay để rời danh sách ActiveSlimes) và trả bộ phím của Slime chính về cả hai bộ
    /// </summary>
    private void ClearHalf()
    {
        if (halfMovement != null)
        {
            halfMovement.gameObject.SetActive(false);
            Destroy(halfMovement.gameObject);
        }
        halfMovement = null;
        if (mainMovement != null) mainMovement.ControlScheme = SlimeControlScheme.Both;
    }
}
