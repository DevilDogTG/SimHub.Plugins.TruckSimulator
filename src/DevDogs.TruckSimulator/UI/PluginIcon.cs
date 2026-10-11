using System.Windows;
using System.Windows.Media;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// The plugin's icon in SimHub's left menu: a semi-truck in side view, drawn as vector paths so it
/// stays sharp at any size and needs no image file. Same paths as <c>docs/images/plugin-icon.svg</c>.
/// </summary>
internal static class PluginIcon
{
    /// <summary>The truck outline on a 24×24 grid; even-odd fill cuts out the window and wheel hubs.</summary>
    private const string TruckPath =
        "F0 M1.5,5.5 h12.5 v9.5 h-12.5 z "
        + "M15,8 h4.6 l3.4,3.9 v3.1 h-8 z "
        + "M16.4,9.4 v2.7 h4.9 l-2.3,-2.7 z "
        + "M1,15.6 h22 v1.4 h-22 z "
        + "M5,16.3 a2.2,2.2 0 1 1 0,4.4 a2.2,2.2 0 1 1 0,-4.4 z M5,17.7 a0.8,0.8 0 1 0 0,1.6 a0.8,0.8 0 1 0 0,-1.6 z "
        + "M10.2,16.3 a2.2,2.2 0 1 1 0,4.4 a2.2,2.2 0 1 1 0,-4.4 z M10.2,17.7 a0.8,0.8 0 1 0 0,1.6 a0.8,0.8 0 1 0 0,-1.6 z "
        + "M19.3,16.3 a2.2,2.2 0 1 1 0,4.4 a2.2,2.2 0 1 1 0,-4.4 z M19.3,17.7 a0.8,0.8 0 1 0 0,1.6 a0.8,0.8 0 1 0 0,-1.6 z";

    /// <summary>
    /// Creates the icon.
    /// </summary>
    /// <returns>A frozen image, safe to share across threads.</returns>
    public static ImageSource Create()
    {
        var drawing = new DrawingGroup();

        // A transparent 24×24 frame keeps the truck's position within the square icon.
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
        drawing.Children.Add(new GeometryDrawing(Brushes.White, null, Geometry.Parse(TruckPath)));

        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
