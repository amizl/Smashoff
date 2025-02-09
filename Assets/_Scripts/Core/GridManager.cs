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

    private Cell[,] grid;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid();
    }
    private void InitializeGrid()
    {
        grid = new Cell[rows, columns];

        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        Vector3 startPosition = new Vector3(
            -gridWidth / 2 + cellSize / 2,
            -gridHeight / 2 + cellSize / 2,
            0
        );

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 position = new Vector3(c * cellSize, r * cellSize, 0) + startPosition;
                GameObject cellObj = Instantiate(cellPrefab, position, Quaternion.identity, CellsDirectory.transform);

                Cell cellComponent = cellObj.GetComponent<Cell>();
                TerrainType terrain = GenerateRandomTerrain(); //Assign random terrain
                cellComponent.Initialize(r, c, terrain); //Pass terrain to cell

                grid[r, c] = cellComponent;
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

    public Vector3 GetWorldPosition(int row, int col)
    {
        if (!IsValidPosition(row, col)) return Vector3.zero;
        return grid[row, col].transform.position;
    }

    public bool IsValidPosition(int row, int col)
    {
        return row >= 0 && row < rows && col >= 0 && col < columns;
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

    // Convert world position to grid coordinates
    public Vector2Int GetGridPositionFromWorld(Vector3 worldPosition)
    {
        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        Vector3 startPosition = new Vector3(
            -gridWidth / 2 + cellSize / 2,
            -gridHeight / 2 + cellSize / 2,
            0
        );

        int col = Mathf.RoundToInt((worldPosition.x - startPosition.x) / cellSize);
        int row = Mathf.RoundToInt((worldPosition.y - startPosition.y) / cellSize);

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
