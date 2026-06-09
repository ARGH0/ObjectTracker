using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public partial class GridEditorDialogViewModel : ObservableObject
{
    private readonly HashSet<GridCell> _selectedCells;
    private readonly IReadOnlyCollection<GridCell> _originalCells;

    [ObservableProperty]
    private int _selectedCellCount;

    public IReadOnlyCollection<GridCell> SelectedCells => _selectedCells;
    public IReadOnlyCollection<GridCell> OriginalCells => _originalCells;

    public GridEditorDialogViewModel(
        int gridColumns,
        int gridRows,
        IReadOnlyCollection<GridCell> currentCells,
        System.Threading.Tasks.Task<byte[]?>? frameLoader)
    {
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

    public bool IsOriginalCell(GridCell cell)
    {
        return _originalCells.Contains(cell);
    }

    public IReadOnlyList<GridCell> GetSaveResult()
    {
        return new List<GridCell>(_selectedCells);
    }
}
