using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Cell : MonoBehaviour, IPointerClickHandler
{
    private TextMeshPro textMeshPro;
    public int Row { get; private set; }
    public int Col { get; private set; }
    public TerrainType Terrain { get; private set; }
    public Vector3 WorldPosition { get; private set; }
    public NetworkUnit OccupyingUnit { get; private set; }

    private SpriteRenderer spriteRenderer; // **NEW: Reference to the sprite renderer**
    private Color originalColor; // **NEW: Store original color**

    public void Initialize(int row, int col, TerrainType terrain)
    {
        this.Row = row;
        this.Col = col;
        this.Terrain = terrain;
        this.WorldPosition = transform.position;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
      //spriteRenderer = GetComponent<SpriteRenderer>(); // **NEW: Get SpriteRenderer**
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color; // **NEW: Store the original color**
        }
        textMeshPro = GetComponentInChildren<TextMeshPro>();
        // Add TextMeshPro to display row and column
        textMeshPro = GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = $"{row},{col}"; // Show grid coordinates
        }
    }

    public void SetOccupyingUnit(NetworkUnit unit)
    {
        OccupyingUnit = unit;
    }

    public bool IsOccupied()
    {
        return OccupyingUnit != null;
    }

    public void ClearOccupant()
    {
        OccupyingUnit = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {//Todo Update the color of selected cells to red if a unit cannot be deployed on that cell.
        GridManager.Instance.DeselectAllCells(); // Reset all cells first
        GridManager.Instance.SetSelectedCell(Row, Col); // Select new one
        HighlightCell(true);
    }

    // **NEW: Highlight cell when selected**
    public void HighlightCell(bool isSelected)
    {
        if (spriteRenderer == null) return;

        if (isSelected)
        {
            spriteRenderer.color = Color.green; // Change to green when selected
        }
        else
        {
            spriteRenderer.color = originalColor; // Revert to original color
        }
    }
    public void SetTileSprite(Sprite sprite) // New!
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
    }

}
