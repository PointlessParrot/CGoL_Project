using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    int xMin, yMin;
    int xLen = 64, yLen = 32;

    int pix => pixSixth * 6;
    int pixHalf => pixSixth * 3;
    int pixSixth = 4;
    
    int iterationTime = 128;
    ObservableString<int> iterations = null!; 
    
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
        while (iterations.Value < 5206)
        {
            iterations.Value++;
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

                iterations.Value++;
            }

            if (cellsMain.Count == 0)
            {
                StartStopIteratingUI(false);
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
        UpdateCoordBlocksOnUI();
        AddBindings();

        Thread programLoopThread = new Thread(ProgramLoop);
        programLoopThread.Start();
    }

    void AddBindings()
    {
        iterations = new ObservableString<int>(0, value => $"Iteration: {value}");
        IterationBlock[!TextBlock.TextProperty] = iterations.GetBinding();
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
        iterations.Value = 0;
        xMin = 0; 
        yMin = 0;
        DrawCellsOnUI(cellCanvas);
        UpdateCoordBlocksOnUI();
    }

    void DimensionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDimensions(WidthSlider.Value, HeightSlider.Value, PixelSlider.Value);
        DrawCellsOnUI(cellCanvas);
        UpdateCoordBlocksOnUI();
    }

    void SaveButton_OnClick(object? sender, RoutedEventArgs e) => 
        SaveState($"{FileBox.Text ?? "save"}.bin");

    void LoadButton_OnClick(object? sender, RoutedEventArgs e) => 
        TryLoadState($"{FileBox.Text ?? "save"}.bin"); 

    void DisplayCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!running)
            PointToggled(e.GetPosition((Visual?)sender));
    }
    
    void DisplayCanvas_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        int change = (int)e.Delta.Y;

        if (isCtrl)
            change *= 16;
        
        if (isShift)
            xMin += change;
        else
            yMin += change;
        
        DrawCellsOnUI(cellCanvas);
        UpdateCoordBlocksOnUI();
    }
    
    void SpeedSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        iterationTime = (int)Math.Pow(2, e.NewValue);
    }

    void SetDimensions(double x, double y, double pixel)
    {
        xMin += xLen / 2;
        yMin += yLen / 2;
        xLen = (int)Math.Pow(2, (int)x);
        yLen = (int)Math.Pow(2, (int)y);
        xMin -= xLen / 2;
        yMin -= yLen / 2;
        
        pixSixth = (int)Math.Pow(2, (int)pixel);
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
    }

    void UpdateCoordBlocksOnUI() => 
        Dispatcher.UIThread.Invoke(UpdateCoordBlocks);
    
    void DrawCellsOnUI(Canvas canvas) => 
        Dispatcher.UIThread.Invoke(() => DrawCells(canvas));

    void FillCellOnUI(Canvas canvas, int x, int y, 
        ImmutableSolidColorBrush? fill = null, ImmutableSolidColorBrush? stroke = null) =>
        Dispatcher.UIThread.Invoke(() => FillCell(canvas, x, y, fill, stroke));

    void EmptyCellOnUI(Canvas canvas, int x, int y) =>
        Dispatcher.UIThread.Invoke(() => EmptyCell(canvas, x, y));

    void UpdateCoordBlocks()
    {
        XCoordsBlock.Text = $"x: {xMin} to {xMin + xLen - 1}";
        YCoordsBlock.Text = $"y: {yMin} to {yMin + yLen - 1}";
    }
    
    void DrawCells(Canvas canvas)
    {
        IEnumerable<Control> cells = canvas.Children.Where(item => (item.Name ?? string.Empty).StartsWith("Cell"));
        canvas.Children.RemoveAll(cells);
        
        
        foreach ((Coordinate coord, int _) in cellsMain.Where(kvp => CoordinateInRange(kvp.Key)))
            FillCellOnUI(canvas, coord.x - xMin, (yLen - coord.y - 1) + yMin);
    }

    void FillCell(Canvas canvas, int x, int y, 
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

    void EmptyCell(Canvas canvas, int x, int y)
    {
        Control? rect = canvas.Children.FirstOrDefault(item => item.Name == $"Cell({x},{y})");
        
        if (rect != null)
            canvas.Children.Remove(rect);
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
    
    //------------------------------------------------------------
    // General 

    static string saveFolderPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "savefiles");
    
    void SaveState(string fileName)
    {
        StartStopIteratingUI(false);
        Thread.Sleep(iterationTime);
        
        if (!Directory.Exists(saveFolderPath))
            Directory.CreateDirectory(saveFolderPath);
        
        string path = System.IO.Path.Combine(saveFolderPath, fileName);    
        using BinaryWriter sw = new BinaryWriter(File.Open(path, FileMode.Create));
        sw.Write7BitEncodedInt(iterations.Value);
        sw.Write7BitEncodedInt(cellsMain.Count);
        foreach ((Coordinate c, int _) in cellsMain)
        {
            sw.Write7BitEncodedInt(c.x);
            sw.Write7BitEncodedInt(c.y);
        }
    }

    void LoadState(string fileName)
    {
        if (!File.Exists(System.IO.Path.Combine(saveFolderPath, fileName)))
            return;
        
        StartStopIteratingUI(false);
        Thread.Sleep(iterationTime);
        
        cellsMain.Clear();
        
        string path = System.IO.Path.Combine(saveFolderPath, fileName);
        using BinaryReader sw = new BinaryReader(File.Open(path, FileMode.Open));
        iterations.Value = sw.Read7BitEncodedInt();
        int count = sw.Read7BitEncodedInt();
        for (int i = 0; i < count; i++)
        {
            int x = sw.Read7BitEncodedInt();
            int y = sw.Read7BitEncodedInt();
            cellsMain.Add(new Coordinate(x, y), 0);
        }
        
        DrawCellsOnUI(cellCanvas);
    }

    void TryLoadState(string fileName)
    {
        if (!File.Exists(System.IO.Path.Combine(saveFolderPath, fileName)))
            return;
        
        LoadState(fileName);
        DrawCellsOnUI(cellCanvas);
    }
    
    //-----------------------------------------------------
    // Testing Code
    
    //-----------------------------------------------------
    // Classes & Structs

    public class ObservableString<T>
    {
        Func<T, string> toString;
        T value;
        Subject<string> source;

        public ObservableString(T value, Func<T, string>? toString = null)
        {
            this.value = value;
            this.toString = toString ??  (x => x!.ToString()!);
            source = new Subject<string>();
            source.OnNext(this.toString(this.value));
        }

        void UpdateValue(T value)
        {
            this.value = value;
            source.OnNext(this.toString(this.value));
        }

        public T Value
        {
            get => value;
            set => UpdateValue(value);
        }

        public IBinding GetBinding() => source.ToBinding();
    }
}
