using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
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
using Thread = System.Threading.Thread;

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
        ToggleCell((31, 15));
        ToggleCell((31, 16));
        ToggleCell((32, 15));
        ToggleCell((32, 16));

        AliveRule = CreateRule(2, 3);
        DeadRule = CreateRule(1, 2);
        
        DrawCellsOnUI(cellCanvas);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (iterations.Value < 200)
        {
            iterations.Value++;
            IterateCells();
            DrawCellsOnUI(cellCanvas);
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
    
    bool benchmark = false;
    
    // Entry Point
    public MainWindow()
    {
        InitializeComponent();
        
        SetupCanvas(cellCanvas);
        UpdateCoordBlocksOnUI();
        AddBindings();

        Thread conwayThread = new Thread(ProgramLoop);
        
        if (benchmark) conwayThread = new Thread(Benchmark);
        
        conwayThread.Start();
    }

    void AddBindings()
    {
        iterations = new ObservableString<int>(0, value => $"Iteration: {value}");
        IterationBlock[!TextBlock.TextProperty] = iterations.GetBinding();
        iterations.Refresh();
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
        SaveState($"{FileBox.Text ?? "save"}", 
            (FileTypeSelector.SelectedItem as ComboBoxItem)!.Content!.ToString()!);

    void LoadButton_OnClick(object? sender, RoutedEventArgs e) => 
        LoadState($"{FileBox.Text ?? "save"}",
            (FileTypeSelector.SelectedItem as ComboBoxItem)!.Content!.ToString()!); 
    
    void RulesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AliveRule = CreateRule((int)AliveMinSlider.Value, (int)AliveMaxSlider.Value);
        DeadRule = CreateRule((int)DeadMinSlider.Value, (int)DeadMaxSlider.Value);
    }

    static Func<int, bool> CreateRule(int min, int max) => neighbours => (neighbours > min - 1) && (neighbours < max + 1);   
    
    void DisplayCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!running)
            PointToggled(e.GetPosition((Visual?)sender));
    }
    
    void DisplayCanvas_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        (int, int) change = (-(int)e.Delta.X, (int)e.Delta.Y);


        if (isCtrl)
        {
            change.Item1 *= 16;
            change.Item2 *= 16;
        }

        if (isShift)
        {
            yMin -= change.Item1;
            xMin += change.Item2;
        }
        else
        {
            xMin += change.Item1;
            yMin += change.Item2;
        }
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
        
        y = yLen - y - 1;
        
        bool alive = ToggleCell((x + xMin, y + yMin));
        
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
            FillCell(canvas, coord.x - xMin, coord.y - yMin);
    }

    void FillCell(Canvas canvas, int x, int y, 
        ImmutableSolidColorBrush? fill = null, ImmutableSolidColorBrush? stroke = null)
    {
        canvas.Children.Add(new Rectangle()
        {
            Name = $"Cell({x},{y})",
            [Canvas.BottomProperty] = y * pix,
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
    
    Coordinate[] adjacentValues = [ (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1) ];
    
    bool ToggleCell(Coordinate coord)
    {
        if (cellsMain.TryAdd(coord, 0)) return true;
        
        cellsMain.Remove(coord);
        return false;

    }

    void Method1(Coordinate coord)
    {
        foreach (Coordinate offset in adjacentValues)
        {
            if (!cellsExtra.TryAdd(coord + offset, 1))
                cellsExtra[coord + offset]++;
        }
    }

    void Method2(KeyValuePair<Coordinate, int> kvp)
    {
        if (!( cellsMain.ContainsKey(kvp.Key) ? AliveRule(kvp.Value) : DeadRule(kvp.Value) ))
            cellsExtra.Remove(kvp.Key);
    }
    
    void IterateCells()
    {
        cellsMain.Keys.ToList().ForEach(Method1);
        
        foreach (var kvp in cellsExtra)
            Method2(kvp);
        
        (cellsMain, cellsExtra) = (cellsExtra, cellsMain);
        cellsExtra.Clear();
    }

    Func<int, bool> AliveRule = CreateRule(2, 3);
    Func<int, bool> DeadRule = CreateRule(3, 3);

    
    //------------------------------------------------------------
    // General 

    string saveFolderPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "savefiles");
    
    void SaveState(string fileName, string fileType)
    {
        StartStopIteratingUI(false);
        Thread.Sleep(iterationTime);
        
        if (!Directory.Exists(saveFolderPath))
            Directory.CreateDirectory(saveFolderPath);
        
        string path = System.IO.Path.Combine(saveFolderPath, $"{fileName}{fileType}");

        switch (fileType)
        {
            case ".bin":
                WriteBinFile(path, cellsMain, iterations.Value);
                break;
            
            case ".life":
                WriteLifeFile(path, cellsMain);
                break;
        }
    }

    void LoadState(string fileName, string fileType)
    {
        StartStopIteratingUI(false);
        Thread.Sleep(iterationTime);
        
        if (!File.Exists(System.IO.Path.Combine(saveFolderPath, $"{fileName}{fileType}")))
            return;
        
        string path = System.IO.Path.Combine(saveFolderPath, $"{fileName}{fileType}");

        cellsMain.Clear();
        
        switch (fileType)
        {
            case ".bin":
                ReadBinFile(path, cellsMain, out int iterationCount);
                iterations.Value = iterationCount;
                break;
            case ".life":
                ReadLifeFile(path, cellsMain);
                iterations.Value = 0;
                break;
        }
        
        DrawCellsOnUI(cellCanvas);
    }

    void WriteBinFile(string path, Dictionary<Coordinate, int> cells, int iterationCount = 0)
    {
        using BinaryWriter binaryWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        binaryWriter.Write7BitEncodedInt(iterationCount);
        binaryWriter.Write7BitEncodedInt(cells.Count);
        foreach ((Coordinate c, int _) in cells)
        {
            binaryWriter.Write7BitEncodedInt(c.x);
            binaryWriter.Write7BitEncodedInt(c.y);
        }
    }

    void ReadBinFile(string path, Dictionary<Coordinate, int> cells, out int iterationCount)
    {
        using BinaryReader binaryReader = new BinaryReader(File.Open(path, FileMode.Open));
        iterationCount = binaryReader.Read7BitEncodedInt();
        int count = binaryReader.Read7BitEncodedInt();
        for (int i = 0; i < count; i++)
        {
            int x = binaryReader.Read7BitEncodedInt();
            int y = binaryReader.Read7BitEncodedInt();
            cells.Add((x, y), 0);
        }
    }

    void WriteLifeFile(string path, Dictionary<Coordinate, int> cells)
    {
        using StreamWriter streamWriter = new StreamWriter(File.Open(path, FileMode.Create));
        streamWriter.WriteLine("#Life 1.06");
        foreach ((Coordinate c, int _) in cells)
            streamWriter.WriteLine($"{c.x} {c.y}");
    }

    void ReadLifeFile(string path, Dictionary<Coordinate, int> cells)
    {
        using StreamReader streamReader = new StreamReader(File.Open(path, FileMode.Open));
        streamReader.ReadLine();
        while (!streamReader.EndOfStream)
        {
            var coords = streamReader.ReadLine()!.Split(' ').Select(int.Parse).ToArray();
            cells.Add((coords[0], coords[1]), 0);
        }
    }
    
    //-----------------------------------------------------
    // Testing Code
    
    //-----------------------------------------------------
    // Classes & Structs

    public class ObservableString<T>
    {
        Func<T, string> toString;
        Subject<string> source;
        T value;

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
            Refresh();
        }

        public T Value
        {
            get => value;
            set => UpdateValue(value);
        }

        public void Refresh() => source.OnNext(toString(value));
        
        public IBinding GetBinding() => source.ToBinding();
    }
}
