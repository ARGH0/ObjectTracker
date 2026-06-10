using System;
using ObjectTracker.UI.Desktop.Region.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public readonly record struct GridEditorResult(IReadOnlyList<GridCell> Cells)
{
    public bool HasSelection => Cells.Count > 0;
}

public partial class GridEditorDialog : Window
{
    private readonly int _gridColumns;
    private readonly int _gridRows;
    private readonly Func<Task<byte[]?>> _frameLoader;
    private double _cellWidth;
    private double _cellHeight;
    private double _offsetX;
    private double _offsetY;
    private readonly Dictionary<(int Col, int Row), Rectangle> _cellRectangles = new();
    private bool _isDragging = false;
    private bool _addMode = true;
    private GridEditorDialogViewModel vm;

    public GridEditorDialog(
        int gridColumns,
        int gridRows,
        IReadOnlyCollection<GridCell> currentCells,
        string layerName,
        Func<Task<byte[]?>> frameLoader)
    {
        _gridColumns = gridColumns;
        _gridRows = gridRows;
        _frameLoader = frameLoader;

        vm = new GridEditorDialogViewModel(gridColumns, gridRows, currentCells, null!);
        DataContext = vm;

        InitializeComponent();
        TitleTextBlock.Text = layerName;
        SaveButton.IsEnabled = vm.SelectedCellCount > 0;

        SaveButton.Click += OnSaveClick;
        CancelButton.Click += OnCancelClick;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(async () =>
        {
            byte[]? jpegBytes = await _frameLoader();
            if (jpegBytes is not null && jpegBytes.Length > 0)
            {
                var bitmap = new Bitmap(new MemoryStream(jpegBytes));
                CameraFrameImage.Source = bitmap;
                CameraFrameImage.Stretch = Stretch.Uniform;
            }
            BuildGridFromContainer();
        }, Avalonia.Threading.DispatcherPriority.ContextIdle);
    }

    private void BuildGridFromContainer()
    {
        var container = CanvasArea;
        var canvasWidth = container.Bounds.Width;
        var canvasHeight = container.Bounds.Height;

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            Dispatcher.UIThread.Post(() => BuildGridFromContainer(), Avalonia.Threading.DispatcherPriority.ContextIdle);
            return;
        }

        var imageWidth = CameraFrameImage.Source is not null ? (int)CameraFrameImage.Source.Size.Width : 640;
        var imageHeight = CameraFrameImage.Source is not null ? (int)CameraFrameImage.Source.Size.Height : 480;

        var scaleW = canvasWidth / imageWidth;
        var scaleH = canvasHeight / imageHeight;
        var scaledWidth = imageWidth * scaleW;
        var scaledHeight = imageHeight * scaleH;
        _offsetX = (canvasWidth - scaledWidth) / 2;
        _offsetY = (canvasHeight - scaledHeight) / 2;

        _cellWidth = scaledWidth / _gridColumns;
        _cellHeight = scaledHeight / _gridRows;

        GridCanvas.Width = canvasWidth;
        GridCanvas.Height = canvasHeight;
        GridCanvas.IsVisible = true;

        BuildGridOverlay();
        UpdateCellVisuals();
    }

    private void BuildGridOverlay()
    {
        GridCanvas.Children.Clear();
        _cellRectangles.Clear();

        for (var row = 0; row < _gridRows; row++)
        {
            for (var col = 0; col < _gridColumns; col++)
            {
                var rect = new Rectangle
                {
                    Width = _cellWidth,
                    Height = _cellHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 200, 200, 200)),
                    StrokeThickness = 0.5,
                    IsHitTestVisible = true,
                };

                Canvas.SetLeft(rect, _offsetX + col * _cellWidth);
                Canvas.SetTop(rect, _offsetY + row * _cellHeight);

                rect.Tag = (col, row);
                GridCanvas.Children.Add(rect);
                _cellRectangles[(col, row)] = rect;
            }
        }
    }

    private void GridCanvasOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        var pos = e.GetPosition(GridCanvas);
        var cell = GetCellAtPosition(pos);
        if (cell.HasValue)
        {
            var gridCell = new GridCell(cell.Value.Col, cell.Value.Row);
            _addMode = !vm.SelectedCells.Contains(gridCell);
            vm.ToggleCell(gridCell);
            UpdateCellVisuals();
        }
    }

    private void GridCanvasOnPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var pos = e.GetPosition(GridCanvas);
        var cell = GetCellAtPosition(pos);
        if (cell.HasValue)
        {
            var gridCell = new GridCell(cell.Value.Col, cell.Value.Row);
            bool isSelected = vm.SelectedCells.Contains(gridCell);
            if ((_addMode && !isSelected) || (!_addMode && isSelected))
            {
                vm.ToggleCell(gridCell);
                UpdateCellVisuals();
            }
        }
    }

    private (int Col, int Row)? GetCellAtPosition(Avalonia.Point pos)
    {
        var col = (int)Math.Floor((pos.X - _offsetX) / _cellWidth);
        var row = (int)Math.Floor((pos.Y - _offsetY) / _cellHeight);
        if (col < 0 || col >= _gridColumns || row < 0 || row >= _gridRows)
            return null;
        return (col, row);
    }

    private void UpdateCellVisuals()
    {
        var vm = (GridEditorDialogViewModel)DataContext!;
        var selectedCells = vm.SelectedCells;
        var originalCells = vm.OriginalCells;

        foreach (var kvp in _cellRectangles)
        {
            var rect = kvp.Value;
            var cell = new GridCell(kvp.Key.Col, kvp.Key.Row);

            if (selectedCells.Contains(cell))
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(115, 255, 0, 0));
            }
            else if (originalCells.Contains(cell))
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(60, 0, 180, 0));
            }
            else
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            }
        }

        CellCountTextBlock.Text = $"{vm.SelectedCellCount} cell{(vm.SelectedCellCount == 1 ? "" : "s")} selected";
        SaveButton.IsEnabled = vm.SelectedCellCount > 0;
    }

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var vm = (GridEditorDialogViewModel)DataContext!;
        var cells = vm.GetSaveResult();
        Close(new GridEditorResult(cells));
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
