using System;

namespace CGoL_App;

public struct Coordinate(int x, int y) : IEquatable<Coordinate>
{
    public int x { get; set; } = x;
    public int y { get; set; } = y;

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