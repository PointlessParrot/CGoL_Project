using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace BasicGolApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        //----------------------------------------------
        
        if (File.Exists("Input.txt"))
            StringInput(File.ReadAllLines("Input.txt"));
        else 
            Sample();
        
        //OutputSetup();
        
        Output();
        Console.WriteLine("Hello World!");
        MainLoop(80);
    }

    /*
    void OutputSetup()
    {
        DisplayGrid.Height = yLen * 8;
        DisplayGrid.Width = xLen * 8;
        DisplayGrid.RowDefinitions = new RowDefinitions(string.Join(",", new string[yLen].Select(_ => "*")));
        DisplayGrid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", new string[xLen].Select(_ => "*")));

        for (int i = 0; i < yLen; i++)
            for (int j = 0; j < xLen; j++)
                DisplayGrid.Children.Add( new CheckBox()
                {
                    Name = $"CheckBox_({i},{j})",
                    [Grid.RowProperty] = i,
                    [Grid.ColumnProperty] = j,
                    IsEnabled = true,
                });
    }
    */
    
    void StringInput(string[] lines)
    {
        int len = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        for (int j = lines[i].Length - 1; j > -1; j--)
            if (lines[i][j] == '#')
                cellsMain.Add((j, len - i - 1), 0);
    }

    void Sample()
    {
        ToggleCell(new Coordinate(3, 1));
        ToggleCell(new Coordinate(3, 2));
        ToggleCell(new Coordinate(3, 3));
        ToggleCell(new Coordinate(2, 3));
        ToggleCell(new Coordinate(1, 2));
    }
    
    //---------------------------------------------------------
    
    static Dictionary<Coordinate, int> cellsMain = new();
    static Dictionary<Coordinate, int> cellsExtra = new();
    
    static int xMin = 0, xLen = 128; 
    static int yMin = 0, yLen = 64;
    
    static (int, int)[] adjacentValues = [ (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1) ];
    
    /*
    void Output()
    {
        string output = "";
        for (int y = yMin + yLen - 1; y > yMin - 1; y--)
        {
            for (int x = xMin; x < xMin + xLen; x++)
            {
                output += cellsMain.ContainsKey((x, y)) ? $" \u2588" : "  ";
            }

            output += '\n';
        }

        var col = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;

        Console.CursorVisible = false;
        Console.Clear();
        Console.Write(output);
        
        Console.ForegroundColor = col;
    }
    */

    void Output()
    {
    }

    void ToggleCell(Coordinate coord)
    {
        if (!cellsMain.TryAdd(coord, 0))
            cellsMain.Remove(coord);
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
    
    void MainLoop(int timeout = 100)
    {
        bool running = true;
        int iteration = 0;
        while (running)
        {
            iteration++;
            
            var stopwatch = Stopwatch.StartNew();
            IterateCells();

            if (stopwatch.ElapsedMilliseconds < timeout)
                Thread.Sleep(timeout - (int)stopwatch.ElapsedMilliseconds);
            
            Output();
        }
    }
}

public struct Coordinate(int x, int y) : IEquatable<Coordinate>
{
    int x { get; set; } = x;
    int y { get; set; } = y;

    public bool Equals(Coordinate other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Coordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y);
    }
    
    public static implicit operator Coordinate((int, int) m) => new(m.Item1, m.Item2);

    public static Coordinate operator +(Coordinate a, Coordinate b) => new(a.x + b.x, a.y + b.y);
}