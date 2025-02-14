using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    private Vector2Int selectedCell = new Vector2Int(-1, -1); // Default: No cell selected
    public GameObject cellPrefab;
    public GameObject CellsDirectory;
    public static GridManager Instance { get; private set; }

    public int rows = 5;
    public int columns = 8;
    public float cellSize = 1f;

    // NEW: Declare an origin variable.
    public Vector3 origin;

    private Cell[,] grid;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid();
    }

    private void InitializeGrid()
    {
        grid = new Cell[columns, rows];

        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        // Calculate the start position of the grid (this will be our origin)
        Vector3 startPosition = new Vector3(
            -gridWidth / 2 + cellSize / 2,
            -gridHeight / 2 + cellSize / 2,
            0
        );

        // NEW: Store startPosition as the origin.
        origin = startPosition;

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                // Calculate each cell's position relative to the origin.
                Vector3 position = new Vector3(x * cellSize, y * cellSize, 0) + startPosition;
                GameObject cellObj = Instantiate(cellPrefab, position, Quaternion.identity, CellsDirectory.transform);

                Cell cellComponent = cellObj.GetComponent<Cell>();
                TerrainType terrain = GenerateRandomTerrain();
                cellComponent.Initialize(x, y, terrain);  // Now x is column, y is row

                grid[x, y] = cellComponent;
            }
        }
    }

    // Generates a random terrain type
    private TerrainType GenerateRandomTerrain()
    {
        float random = Random.value;
        if (random < 0.2f) return TerrainType.DefenseBonus;
        if (random < 0.4f) return TerrainType.AttackBonus;
        if (random < 0.6f) return TerrainType.ResourceGen;
        if (random < 0.8f) return TerrainType.Healing;
        return TerrainType.Normal;
    }

    // NEW: Update GetWorldPosition to calculate from origin.
    // (x, y) are the grid coordinates.
    public Vector3 GetWorldPosition(int x, int y)
    {
        // You can either return the precomputed cell position...
        // if (!IsValidPosition(y, x)) return Vector3.zero;
        // return grid[x, y].transform.position;

        // ...or calculate it using origin and cellSize:
        return origin + new Vector3(x * cellSize, y * cellSize, 0);
    }

    public bool IsValidPosition(int x, int y)
    {
     //   return row >= 0 && row < rows && col >= 0 && col < columns;
        return x >= 0 && x < columns && y >= 0 && y < rows;
    }

    public Cell GetCell(int row, int col)
    {
        if (!IsValidPosition(row, col)) return null;
        return grid[row, col];
    }

    public void SetSelectedCell(int row, int col)
    {
        selectedCell = new Vector2Int(row, col);
    }

    public Vector2Int GetSelectedCell()
    {
        return selectedCell;
    }

    // Convert world position to grid coordinates using the origin.
    public Vector2Int GetGridPositionFromWorld(Vector3 worldPosition)
    {
        // Use the origin we stored during initialization.
        int col = Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize);
        int row = Mathf.RoundToInt((worldPosition.y - origin.y) / cellSize);

        if (IsValidPosition(row, col))
            return new Vector2Int(row, col);

        return new Vector2Int(-1, -1); // Invalid position
    }

    public void DeselectAllCells()
    {
        foreach (var cell in FindObjectsByType<Cell>(FindObjectsSortMode.None))
            cell.HighlightCell(false);
    }
}
