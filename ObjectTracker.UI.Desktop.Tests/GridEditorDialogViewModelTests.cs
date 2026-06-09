using System.Collections.Generic;
using System.Linq;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class GridEditorDialogViewModelTests
{
    [Fact]
    public void ToggleCell_AddsUnselectedCellToSelection()
    {
        var cells = new[] { new GridCell(0, 0), new GridCell(1, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        var cellToToggle = new GridCell(2, 0);

        vm.ToggleCell(cellToToggle);

        Assert.Contains(cellToToggle, vm.SelectedCells);
    }

    [Fact]
    public void ToggleCell_RemovesAlreadySelectedCell()
    {
        var cells = new[] { new GridCell(0, 0), new GridCell(1, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        var cellToRemove = new GridCell(0, 0);

        vm.ToggleCell(cellToRemove);

        Assert.DoesNotContain(cellToRemove, vm.SelectedCells);
    }

    [Fact]
    public void SelectedCells_ReflectsCurrentStateAfterMultipleToggles()
    {
        var cells = new[] { new GridCell(0, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        vm.ToggleCell(new GridCell(0, 0));
        vm.ToggleCell(new GridCell(1, 0));

        Assert.Single(vm.SelectedCells);
        Assert.Contains(new GridCell(1, 0), vm.SelectedCells);
        Assert.DoesNotContain(new GridCell(0, 0), vm.SelectedCells);
    }

    [Fact]
    public void GetSaveResult_ReturnsSelectedCellsAsImmutableList()
    {
        var cells = new GridCell[] { new(0, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        vm.ToggleCell(new GridCell(1, 0));
        vm.ToggleCell(new GridCell(2, 0));

        var result = vm.GetSaveResult();

        Assert.Equal(3, result.Count);
        Assert.Contains(new GridCell(0, 0), result);
        Assert.Contains(new GridCell(1, 0), result);
        Assert.Contains(new GridCell(2, 0), result);
    }

    [Fact]
    public void SelectedCellCount_UpdatesOnToggle()
    {
        var cells = new GridCell[] { new(0, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        Assert.Equal(1, vm.SelectedCellCount);

        vm.ToggleCell(new GridCell(1, 0));
        Assert.Equal(2, vm.SelectedCellCount);

        vm.ToggleCell(new GridCell(0, 0));
        Assert.Equal(1, vm.SelectedCellCount);
    }

    [Fact]
    public void IsOriginalCell_ReturnsTrueForCellsInEditedRegion()
    {
        var cells = new[] { new GridCell(0, 0), new GridCell(1, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        Assert.True(vm.IsOriginalCell(new GridCell(0, 0)));
        Assert.True(vm.IsOriginalCell(new GridCell(1, 0)));
    }

    [Fact]
    public void IsOriginalCell_ReturnsFalseForCellsNotInEditedRegion()
    {
        var cells = new[] { new GridCell(0, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        Assert.False(vm.IsOriginalCell(new GridCell(2, 0)));
    }

    [Fact]
    public void OriginalCells_ReturnsSameCollectionAsCurrentCells()
    {
        var cells = new[] { new GridCell(0, 0), new GridCell(1, 0) };

        var vm = new GridEditorDialogViewModel(
            gridColumns: 4,
            gridRows: 3,
            currentCells: cells,
            frameLoader: null!);

        Assert.Equal(2, vm.OriginalCells.Count);
        Assert.Contains(new GridCell(0, 0), vm.OriginalCells);
        Assert.Contains(new GridCell(1, 0), vm.OriginalCells);
    }
}
