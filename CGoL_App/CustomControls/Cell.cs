using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace CGoL_App.CustomControls;

public class Cell : Control 
{
    public IBrush? AliveColour { get; set; } = Brushes.LimeGreen;
    public IBrush? DeadColour { get; set; } = Brushes.Transparent;

    public bool IsAlive { get; set; }
    
    public Coordinate Coordinate { get; set; }
    
    public sealed override void Render(DrawingContext dc)
    {
        if (IsAlive && AliveColour != null)
        {
            var renderSize = Bounds.Size;
            dc.FillRectangle(AliveColour, new Rect(renderSize));
        }
        else if (DeadColour != null)
        {
            var renderSize = Bounds.Size;
            dc.FillRectangle(DeadColour, new Rect(renderSize));
        }

        base.Render(dc);
    }
}