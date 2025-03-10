public static class DeploymentValidator
{
    /// <summary>
    /// Returns true if the given cell is valid for deployment for the specified player,
    /// based on the cell’s x-coordinate (stored in Row), spawn zone restrictions, and available resources.
    /// </summary>
    public static bool CanDeployUnit(Cell cell, Player currentPlayer, int totalCost)
    {
        // Cell must exist and not be occupied.
        if (cell == null || cell.IsOccupied())
            return false;

        // Use the x-coordinate stored in cell.Row for spawn zone checks.
        int col = cell.Row;

        if (currentPlayer == Player.Player1)
        {
            // Player 1 can only deploy in columns 0 to 2.
            if (col < 0 || col > 2)
                return false;
        }
        else // Player 2
        {
            int totalColumns = GridManager.Instance.columns;
            // Player 2 can only deploy in the last three columns.
            if (col < totalColumns - 3 || col >= totalColumns)
                return false;
        }

        // Finally, check if the player has enough resources.
        if (ResourceManager.Instance.GetResources(currentPlayer) < totalCost)
            return false;

        return true;
    }
}
