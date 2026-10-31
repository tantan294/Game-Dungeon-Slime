using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hình dạng Jelly mà nút nhấn yêu cầu (Any = hình gì cũng được)
/// </summary>
public enum PlateShape
{
    Any,
    Square,
    Tall,
    TallSlim,
    Flat,
    FlatSlim
}

public static class PlateShapeExtensions
{
    /// <summary>
    /// Hình dạng Jelly tương ứng (null = Any)
    /// </summary>
    public static SlimeShape? ToSlimeShape(this PlateShape shape)
    {
        switch (shape)
        {
            case PlateShape.Square: return SlimeShape.Square_4x4;
            case PlateShape.Tall: return SlimeShape.Tall_2x6;
            case PlateShape.TallSlim: return SlimeShape.TallSlim_1x8;
            case PlateShape.Flat: return SlimeShape.Flat_6x2;
            case PlateShape.FlatSlim: return SlimeShape.FlatSlim_8x1;
            default: return null;
        }
    }
}

/// <summary>
/// Nút nhấn (Pressure Plate): được tính là "đang nhấn" khi có một khối/nửa Jelly đứng yên đè lên nút
/// với đúng hình dạng yêu cầu. Khi tất cả nút cùng kênh được nhấn CÙNG LÚC, các cửa (PlateDoor) cùng kênh mở ra.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    public static readonly List<PressurePlate> All = new List<PressurePlate>();

    [Tooltip("Kênh liên kết với cửa (PlateDoor) cùng số")]
    [SerializeField] private int channel = 1;

    [Tooltip("Hình dạng Jelly cần có để nhấn nút")]
    [SerializeField] private PlateShape requiredShape = PlateShape.Any;

    [Tooltip("Kích thước vùng nút (đơn vị thế giới)")]
    [SerializeField] private Vector2 size = new Vector2(0.5f, 0.5f);

    [Tooltip("Vùng nhận diện lùi vào mỗi phía để chạm mép không tính")]
    [SerializeField] private float inset = 0.1f;

    [SerializeField] private SpriteRenderer plateRenderer;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color pressedColor = Color.white;

    public bool IsPressed { get; private set; }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Update()
    {
        bool pressed = CheckPressed();
        if (pressed == IsPressed) return;

        IsPressed = pressed;
        if (plateRenderer != null)
        {
            plateRenderer.color = IsPressed ? pressedColor : idleColor;
        }
    }

    private bool CheckPressed()
    {
        Vector2 center = transform.position;
        Vector2 area = size - Vector2.one * (inset * 2f);
        Rect zone = new Rect(center - area * 0.5f, area);

        foreach (var slime in SlimeMovement.ActiveSlimes)
        {
            if (slime == null || slime.IsSliding) continue;
            if (!zone.Overlaps(slime.OccupiedRect)) continue;

            SlimeShape? need = requiredShape.ToSlimeShape();
            if (slime.Shifter != null && (need == null || need == slime.Shifter.CurrentShape)) return true;
        }
        return false;
    }

    /// <summary>
    /// Tất cả nút thuộc kênh này có đang được nhấn cùng lúc không (kênh không có nút -> false)
    /// </summary>
    public static bool AllPressed(int channel)
    {
        bool any = false;
        foreach (var plate in All)
        {
            if (plate.channel != channel) continue;
            any = true;
            if (!plate.IsPressed) return false;
        }
        return any;
    }
}
