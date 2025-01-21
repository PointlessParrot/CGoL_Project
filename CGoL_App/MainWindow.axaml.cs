using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CGoL_App.CustomControls;

namespace CGoL_App;

public partial class MainWindow : Window
{
    static int xMin = 0, yMin = 0;
    static int xLen = 64, yLen = 32;
    
    static int pix = 20;
    static int pixHalf => pix / 2;
    
    static bool running = false;
    
    void ProgramLoop()
    {
        
    }
    
    //--------------------------------------------
    // Setup
    
    // Entry Point
    public MainWindow()
    {
        InitializeComponent();
        
        SetupCanvas(DisplayCanvas);

        Thread programLoopThread = new Thread(ProgramLoop);
        programLoopThread.Start();
    }

    /*
    static void SetupGrid(Grid cellGrid)
    {
        cellGrid.Height = yLen * 4;
        cellGrid.Width = xLen * 4;
        cellGrid.RowDefinitions = new RowDefinitions(string.Join(",", new string[yLen].Select(_ => "*")));
        cellGrid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", new string[xLen].Select(_ => "*")));

        for (int i = 0; i < yLen; i++)
            for (int j = 0; j < xLen; j++)
                cellGrid.Children.Add(new Cell
                { 
                    Name = $"Cell({i},{j})",
                    [Grid.RowProperty] = yLen - i,
                    [Grid.ColumnProperty] = 1 + j,
                });
    }
    */

    static void SetupCanvas(Canvas cellCanvas)
    {
        cellCanvas.Height = yLen * pix;
        cellCanvas.Width = xLen * pix;

        Border canvasBorder = (Border)cellCanvas.Parent!;
        canvasBorder.Height = yLen * pix + 6;
        canvasBorder.Width = xLen * pix + 6;
        
        cellCanvas.Children.Clear();
        
        for (int i = 0; i < (yLen + 1) * pix; i += pix)  
            cellCanvas.Children.Add(new Line()
            {
                StartPoint = new Point(0, i),
                EndPoint = new Point(xLen * pix, i),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.2,
            });
        
        for (int j = 0; j < (xLen + 1) * pix; j += pix)  
            cellCanvas.Children.Add(new Line()
            {
                StartPoint = new Point(j, 0),
                EndPoint = new Point(j, yLen * pix),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.2,
            });
    }

    //--------------------------------------------------
    // Avalonia Logic

    void StepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IterateCells();
    }
    
    void DisplayCanvas_OnTapped(object? sender, TappedEventArgs e)
    {
        if (!running)
            PointToggled(e.GetPosition((Visual?)sender));
    }

    void DisplayCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!running)
            PointToggled(e.GetPosition((Visual?)sender));
    }

    void DimensionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HeightBox.Text, out int height) 
            || !int.TryParse(WidthBox.Text, out int width))
            return;
        SetDimensions(width, height);
    }

    void SetDimensions(int x, int y)
    {
        xLen = x;
        yLen = y;
        SetupCanvas(DisplayCanvas);
        
    }
    
    void PointToggled(Point p)
    {
        p.Deconstruct(out double xRaw, out double yRaw);
        int x = (int)Math.Round((xRaw - pixHalf) / pix);
        int y = (int)Math.Round((yRaw - pixHalf) / pix);
        
        bool alive = ToggleCell((x + xMin, (yLen - y - 1) + yMin));
        
        if (alive)
            FillCell(DisplayCanvas, x, y);
        else
            EmptyCell(DisplayCanvas, x, y);
    }

    void FillCell(Canvas canvas, int x, int y, ImmutableSolidColorBrush? fill = null, ImmutableSolidColorBrush? stroke = null)
    {
        canvas.Children.Add(new Rectangle()
        {
            Name = $"Cell({x},{y})",
            [Canvas.TopProperty] = y * pix,
            [Canvas.LeftProperty] = x * pix,
            Height = pix,
            Width = pix,
            Fill = fill ?? Brushes.LimeGreen ,
            Stroke = stroke ?? Brushes.ForestGreen,
            StrokeThickness = 0.2,
        });
    }

    void EmptyCell(Canvas canvas, int x, int y)
    {
        Control? rect = canvas.Children.FirstOrDefault(item => item.Name == $"Cell({x},{y})");
        
        if (rect != null)
            canvas.Children.Remove(rect);
    }

    void DrawCells(Canvas canvas)
    {
        IEnumerable<Control> cells = canvas.Children.Where(item => (item.Name ?? string.Empty).StartsWith("Cell"));
        canvas.Children.RemoveAll(cells);

        foreach ((Coordinate coord, int _) in cellsMain)
            FillCell(canvas, coord.x, coord.y);
    }

    //---------------------------------------------------
    // Conway Logic
    
    static Dictionary<Coordinate, int> cellsMain = new();
    static Dictionary<Coordinate, int> cellsExtra = new();
    
    static (int, int)[] adjacentValues = [ (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1) ];
    
    bool ToggleCell(Coordinate coord)
    {
        if (cellsMain.TryAdd(coord, 0)) return true;
        
        cellsMain.Remove(coord);
        return false;

    }
    
    void IterateCells()
    {
        foreach (Coordinate coord in cellsMain.Keys.ToList())
        foreach (var offset in adjacentValues)
            if(!cellsExtra.TryAdd(coord + offset, 1))
                cellsExtra[coord + offset] += 1;

        foreach ((Coordinate coord, int neighbours) in cellsExtra.ToList())
            if (!( cellsMain.ContainsKey(coord) ? AliveRule(neighbours) : DeadRule(neighbours) ))
                cellsExtra.Remove(coord);

        cellsMain.Clear();
        cellsMain = cellsExtra.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        cellsExtra.Clear();
    }

    bool AliveRule(int neighbours) => neighbours is 2 or 3;

    bool DeadRule(int neighbours) => neighbours is 3;
    
    void SaveState(string path)
    {
        using BinaryWriter sw = new BinaryWriter(File.Open(path, FileMode.Create));
        sw.Write7BitEncodedInt(cellsMain.Count);
        foreach ((Coordinate c, int _) in cellsMain)
        {
            sw.Write7BitEncodedInt(c.x);
            sw.Write7BitEncodedInt(c.y);
        }
    }

    void LoadState(string path)
    {
        cellsMain.Clear();
        
        using BinaryReader sw = new BinaryReader(File.Open(path, FileMode.Open));
        int count = sw.Read7BitEncodedInt();
        for (int i = 0; i < count; i++)
        {
            int x = sw.Read7BitEncodedInt();
            int y = sw.Read7BitEncodedInt();
            cellsMain.Add(new Coordinate(x, y), 0);
        }
    }

    bool TryLoadState(string path)
    {
        if (!File.Exists(path))
            return false;
        
        LoadState(path);
        return true;
    }
}