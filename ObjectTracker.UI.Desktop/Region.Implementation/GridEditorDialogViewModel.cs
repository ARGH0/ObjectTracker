using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public partial class GridEditorDialogViewModel : ObservableObject
{
    private readonly int _gridColumns;
    private readonly int _gridRows;
    private readonly HashSet<GridCell> _selectedCells;
    private readonly IReadOnlyCollection<GridCell> _originalCells;

    [ObservableProperty]
    private int _selectedCellCount;

    [ObservableProperty]
    private int _pencilSize = 1;

    public IReadOnlyCollection<GridCell> SelectedCells => _selectedCells;
    public IReadOnlyCollection<GridCell> OriginalCells => _originalCells;
    public IReadOnlyList<int> PencilSizes { get; } = new[] { 1, 4, 9, 16, 20, 32, 49};

    public GridEditorDialogViewModel(
        int gridColumns,
        int gridRows,
        IReadOnlyCollection<GridCell> currentCells,
        System.Threading.Tasks.Task<byte[]?>? frameLoader)
    {
        _gridColumns = gridColumns;
        _gridRows = gridRows;
        _originalCells = currentCells;
        _selectedCells = new HashSet<GridCell>(currentCells);
        SelectedCellCount = _selectedCells.Count;
    }

    public void ToggleCell(GridCell cell)
    {
        if (_selectedCells.Contains(cell))
        {
            _selectedCells.Remove(cell);
        }
        else
        {
            _selectedCells.Add(cell);
        }

        SelectedCellCount = _selectedCells.Count;
    }

    public void ApplyPencil(GridCell cell, bool addMode)
    {
        foreach (var pencilCell in GetPencilCells(cell))
        {
            if (addMode)
            {
                _selectedCells.Add(pencilCell);
            }
            else
            {
                _selectedCells.Remove(pencilCell);
            }
        }

        SelectedCellCount = _selectedCells.Count;
    }

    public IReadOnlyList<GridCell> GetPencilCells(GridCell cell)
    {
        var sideLength = PencilSize switch
        {
            4 => 2,
            9 => 3,
            16 => 4,
            20 => 5,
            32 => 6,
            49 => 7,
            _ => 1
        };
        var cells = new List<GridCell>(PencilSize);

        for (var rowOffset = 0; rowOffset < sideLength; rowOffset++)
        {
            for (var colOffset = 0; colOffset < sideLength; colOffset++)
            {
                var column = cell.Column + colOffset;
                var row = cell.Row + rowOffset;
                if (column < _gridColumns && row < _gridRows)
                {
                    cells.Add(new GridCell(column, row));
                }
            }
        }

        return cells;
    }

    public bool IsOriginalCell(GridCell cell)
    {
        return _originalCells.Contains(cell);
    }

    public IReadOnlyList<GridCell> GetSaveResult()
    {
        return new List<GridCell>(_selectedCells);
    }
}
