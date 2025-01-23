using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CGoL_App;

public partial class MainWindow : Window
{
    int xMin = 0, yMin = 0;
    int xLen = 64, yLen = 32;

    int pix { get => pixHalf * 2; set => pixHalf = value / 2; }
    int pixHalf = 6;
    
    int iterationTime = 100;
    int iterations; 
    Subject<string> iterationOutput = new();
    
    bool running;

    Canvas cellCanvas => DisplayCanvas;

    void Benchmark()
    {
        ToggleCell((0, 0));
        ToggleCell((1, 0));
        ToggleCell((4, 0));
        ToggleCell((5, 0));
        ToggleCell((6, 0));
        ToggleCell((1, 2));
        ToggleCell((3, 1));
        
        DrawCellsOnUI(cellCanvas);

        Stopwatch stopwatch = Stopwatch.StartNew();
        int iterations = 0;
        while (iterations < 5206)
        {
            iterations++;
            IterateCells();
        }
        stopwatch.Stop();
        Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
    }
    
    void ProgramLoop()
    {
        while(true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            if (running)
            {
                IterateCells();
                DrawCellsOnUI(cellCanvas);
                
                iterations++;
                iterationOutput.OnNext($"Iteration: {iterations}");
            }

            if (cellsMain.Count == 0)
            {
                StartStopIteratingUI(false);
                iterations = 0;
            }
            
            WaitUntil(stopwatch, iterationTime);
        }
    }

    void WaitUntil(Stopwatch stopwatch, int milliseconds)
    {
        while (stopwatch.ElapsedMilliseconds < milliseconds - 5)
            Thread.Sleep(5);
    }

    void StartStopIteratingUI(bool start) =>
        Dispatcher.UIThread.Invoke(() => StartStopIteratingRaw(start));
    
    void StartStopIteratingRaw(bool start)
    {
        StartButton.IsEnabled = !start;
        StepButton.IsEnabled = !start;
        ResetButton.IsEnabled = !start;
        DimensionsButton.IsEnabled = !start;
        cellCanvas.IsEnabled = !start;
        
        StopButton.IsEnabled = start;
        
        running = start;
    }
    
    //--------------------------------------------
    // Setup
    
    // Entry Point
    public MainWindow()
    {
        InitializeComponent();
        
        SetupCanvas(cellCanvas);
        AddBindings();

        Thread programLoopThread = new Thread(new ThreadStart(ProgramLoop));
        programLoopThread.Start();
    }

    void AddBindings()
    {
        iterationOutput.OnNext("Iteration: 0");
        IterationBox[!TextBox.TextProperty] = iterationOutput.ToBinding();
    }

    void SetupCanvas(Canvas canvas)
    {
        canvas.Height = yLen * pix;
        canvas.Width = xLen * pix;

        Border canvasBorder = (Border)canvas.Parent!;
        canvasBorder.Height = yLen * pix + 4;
        canvasBorder.Width = xLen * pix + 4;
        
        canvas.Children.Clear();
        
        for (int i = 0; i < (yLen + 1) * pix; i += pix)  
            canvas.Children.Add(new Line()
            {
                StartPoint = new Point(0, i),
                EndPoint = new Point(xLen * pix, i),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.2,
            });
        
        for (int j = 0; j < (xLen + 1) * pix; j += pix)  
            canvas.Children.Add(new Line()
            {
                StartPoint = new Point(j, 0),
                EndPoint = new Point(j, yLen * pix),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.2,
            });
    }

    //--------------------------------------------------
    // Avalonia Logic
    
    void SplitViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SplitView? view = ((Control?)sender)?.FindAncestorOfType<SplitView>();
        if (view != null) view.IsPaneOpen = !view.IsPaneOpen;
    }

    void StepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IterateCells();
        DrawCellsOnUI(cellCanvas);
    }

    void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StartStopIteratingRaw(true);
    }

    void StopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StartStopIteratingRaw(false);
    }

    void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        cellsMain.Clear();
        DrawCellsOnUI(cellCanvas);
        iterations = 0;
        iterationOutput.OnNext("Iteration: 0");
    }

    void DisplayCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!running)
            PointToggled(e.GetPosition((Visual?)sender));
    }

    void DimensionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDimensions(WidthSlider.Value, HeightSlider.Value, PixelSlider.Value);
        DrawCellsOnUI(cellCanvas);
    }

    void SetDimensions(double x, double y, double pixel)
    {
        xLen = (int)Math.Pow(2, (int)x);
        yLen = (int)Math.Pow(2, (int)y);
        pix = 3 * (int)Math.Pow(2, (int)pixel);
        SetupCanvas(cellCanvas);
    }
    
    void PointToggled(Point p)
    {
        p.Deconstruct(out double xRaw, out double yRaw);
        int x = (int)Math.Round((xRaw - pixHalf) / pix);
        int y = (int)Math.Round((yRaw - pixHalf) / pix);
        
        bool alive = ToggleCell((x + xMin, (yLen - y - 1) + yMin));
        
        if (alive)
            FillCellOnUI(cellCanvas, x, y);
        else
            EmptyCellOnUI(cellCanvas, x, y);
        
        Console.WriteLine("Toggled");
    }

    void DrawCellsOnUI(Canvas canvas) =>
        Dispatcher.UIThread.Invoke(() => DrawCellsRaw(canvas));
    

    void FillCellOnUI(Canvas canvas, int x, int y, 
        ImmutableSolidColorBrush? fill = null, ImmutableSolidColorBrush? stroke = null) =>
        Dispatcher.UIThread.Invoke(() => FillCellRaw(canvas, x, y, fill, stroke));
    

    void EmptyCellOnUI(Canvas canvas, int x, int y) =>
        Dispatcher.UIThread.Invoke(() => EmptyCellRaw(canvas, x, y));
    

    void FillCellRaw(Canvas canvas, int x, int y, 
        ImmutableSolidColorBrush? fill = null, ImmutableSolidColorBrush? stroke = null)
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

    void EmptyCellRaw(Canvas canvas, int x, int y)
    {
        Control? rect = canvas.Children.FirstOrDefault(item => item.Name == $"Cell({x},{y})");
        
        if (rect != null)
            canvas.Children.Remove(rect);
    }

    void DrawCellsRaw(Canvas canvas)
    {
        IEnumerable<Control> cells = canvas.Children.Where(item => (item.Name ?? string.Empty).StartsWith("Cell"));
        canvas.Children.RemoveAll(cells);
        
        foreach ((Coordinate coord, int _) in cellsMain.Where(kvp => CoordinateInRange(kvp.Key)))
            FillCellOnUI(canvas, coord.x + xMin, (yLen - coord.y - 1) + yMin);
    }

    bool CoordinateInRange(Coordinate coord)
    {
        return coord.x >= xMin && coord.x <= xMin + xLen - 1 && coord.y >= yMin && coord.y <= yMin + yLen - 1;
    }


    //---------------------------------------------------
    // Conway Logic
    
    Dictionary<Coordinate, int> cellsMain = new();
    Dictionary<Coordinate, int> cellsExtra = new();
    
    (int, int)[] adjacentValues = [ (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1) ];
    
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
    
    //-----------------------------------------------------
    // Testing Code
}
