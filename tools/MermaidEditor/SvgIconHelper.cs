using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using Image = System.Windows.Controls.Image;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace MermaidEditor;

/// <summary>
/// Helper class to load SVG icons from embedded resources and convert them to WPF Image elements.
/// Parses SVG path data and renders using WPF geometry, with support for custom fill colors.
/// </summary>
public static class SvgIconHelper
{
    private static readonly Dictionary<string, DrawingImage> _cache = new();

    /// <summary>
    /// Creates a WPF Image element from an embedded SVG resource.
    /// </summary>
    /// <param name="svgFileName">The SVG filename (e.g., "new-file.svg")</param>
    /// <param name="size">The width/height of the icon (default 16)</param>
    /// <param name="fillBrush">The brush to use for filling paths (default foreground theme color)</param>
    /// <returns>A WPF Image element, or null if the SVG could not be loaded</returns>
    public static Image? CreateIcon(string svgFileName, double size = 16, Brush? fillBrush = null)
    {
        var drawing = GetDrawingImage(svgFileName, fillBrush);
        if (drawing == null) return null;

        return new Image
        {
            Source = drawing,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform
        };
    }

    /// <summary>
    /// Gets or creates a DrawingImage from an embedded SVG resource.
    /// </summary>
    public static DrawingImage? GetDrawingImage(string svgFileName, Brush? fillBrush = null)
    {
        var brush = fillBrush ?? new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1)); // Default light text color
        var cacheKey = $"{svgFileName}_{brush}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var svgContent = LoadEmbeddedSvg(svgFileName);
        if (svgContent == null) return null;

        var drawing = ParseSvgToDrawing(svgContent, brush);
        if (drawing == null) return null;

        var drawingImage = new DrawingImage(drawing);
        drawingImage.Freeze();
        _cache[cacheKey] = drawingImage;
        return drawingImage;
    }

    /// <summary>
    /// Loads SVG content from embedded resources.
    /// </summary>
    private static string? LoadEmbeddedSvg(string svgFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".{svgFileName}", StringComparison.OrdinalIgnoreCase)
                              || n.EndsWith($".Icons.{svgFileName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parses SVG XML content and creates a WPF DrawingGroup.
    /// Supports path elements with d attributes, rect elements, polygon elements, and line/polyline elements.
    /// </summary>
    private static DrawingGroup? ParseSvgToDrawing(string svgContent, Brush fillBrush)
    {
        try
        {
            var doc = XDocument.Parse(svgContent);
            var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;

            var viewBox = doc.Root?.Attribute("viewBox")?.Value;
            if (viewBox == null) return null;

            var parts = viewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return null;

            var vbX = double.Parse(parts[0]);
            var vbY = double.Parse(parts[1]);
            var vbWidth = double.Parse(parts[2]);
            var vbHeight = double.Parse(parts[3]);

            var group = new DrawingGroup();

            // Process all path, rect, polygon, polyline, line, circle, ellipse elements recursively
            ProcessElement(doc.Root!, svgNs, group, fillBrush);

            // Set the viewport to match the SVG viewBox
            group.ClipGeometry = new RectangleGeometry(new Rect(vbX, vbY, vbWidth, vbHeight));

            return group;
        }
        catch
        {
            return null;
        }
    }

    private static void ProcessElement(XElement element, XNamespace ns, DrawingGroup group, Brush defaultFill)
    {
        var localName = element.Name.LocalName.ToLowerInvariant();

        // Check for transform attribute
        var transformAttr = element.Attribute("transform")?.Value;
        DrawingGroup? transformGroup = null;
        if (transformAttr != null)
        {
            var transform = ParseTransform(transformAttr);
            if (transform != null)
            {
                transformGroup = new DrawingGroup { Transform = transform };
                group.Children.Add(transformGroup);
                group = transformGroup;
            }
        }

        // Determine fill for this element
        var elementFill = GetElementFill(element, defaultFill);

        // Determine stroke
        var strokeBrush = GetStrokeBrush(element);
        var strokeThickness = GetStrokeThickness(element);
        var strokePen = strokeBrush != null ? new Pen(strokeBrush, strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        } : null;

        switch (localName)
        {
            case "path":
                var d = element.Attribute("d")?.Value;
                if (d != null)
                {
                    try
                    {
                        var geometry = Geometry.Parse(d);
                        var fillRule = element.Attribute("fill-rule")?.Value;
                        if (fillRule == "evenodd" && geometry is PathGeometry pg)
                        {
                            pg.FillRule = FillRule.EvenOdd;
                        }
                        else if (fillRule == "evenodd" && geometry is StreamGeometry)
                        {
                            // Convert to PathGeometry to set FillRule
                            var converted = geometry.GetFlattenedPathGeometry();
                            converted.FillRule = FillRule.EvenOdd;
                            geometry = converted;
                        }

                        // Check for fill="none" - if so, only stroke
                        var fillAttr = element.Attribute("fill")?.Value;
                        Brush? pathFill = fillAttr?.ToLowerInvariant() == "none" ? null : elementFill;

                        group.Children.Add(new GeometryDrawing(pathFill, strokePen, geometry));
                    }
                    catch { /* Skip malformed paths */ }
                }
                break;

            case "rect":
                var rx = ParseDouble(element.Attribute("x")?.Value, 0);
                var ry = ParseDouble(element.Attribute("y")?.Value, 0);
                var rw = ParseDouble(element.Attribute("width")?.Value, 0);
                var rh = ParseDouble(element.Attribute("height")?.Value, 0);
                if (rw > 0 && rh > 0)
                {
                    var fillAttr = element.Attribute("fill")?.Value;
                    // Skip transparent fill rects (common backgrounds)
                    if (fillAttr?.ToLowerInvariant() == "white" && element.Attribute("fill-opacity")?.Value == "0.01")
                        break;
                    Brush? rectFill = fillAttr?.ToLowerInvariant() == "none" ? null : elementFill;
                    group.Children.Add(new GeometryDrawing(rectFill, strokePen,
                        new RectangleGeometry(new Rect(rx, ry, rw, rh))));
                }
                break;

            case "polygon":
                var points = element.Attribute("points")?.Value;
                if (points != null)
                {
                    try
                    {
                        var polyGeom = ParsePolygonPoints(points);
                        if (polyGeom != null)
                            group.Children.Add(new GeometryDrawing(elementFill, strokePen, polyGeom));
                    }
                    catch { }
                }
                break;

            case "circle":
                var cx = ParseDouble(element.Attribute("cx")?.Value, 0);
                var cy = ParseDouble(element.Attribute("cy")?.Value, 0);
                var r = ParseDouble(element.Attribute("r")?.Value, 0);
                if (r > 0)
                {
                    var fillAttr = element.Attribute("fill")?.Value;
                    Brush? circleFill = fillAttr?.ToLowerInvariant() == "none" ? null : elementFill;
                    group.Children.Add(new GeometryDrawing(circleFill, strokePen,
                        new EllipseGeometry(new Point(cx, cy), r, r)));
                }
                break;

            case "line":
                var x1 = ParseDouble(element.Attribute("x1")?.Value, 0);
                var y1 = ParseDouble(element.Attribute("y1")?.Value, 0);
                var x2 = ParseDouble(element.Attribute("x2")?.Value, 0);
                var y2 = ParseDouble(element.Attribute("y2")?.Value, 0);
                if (strokePen != null)
                {
                    group.Children.Add(new GeometryDrawing(null, strokePen,
                        new LineGeometry(new Point(x1, y1), new Point(x2, y2))));
                }
                break;
        }

        // Recurse into child elements
        foreach (var child in element.Elements())
        {
            ProcessElement(child, ns, group, defaultFill);
        }
    }

    private static Brush GetElementFill(XElement element, Brush defaultFill)
    {
        var fillAttr = element.Attribute("fill")?.Value;
        if (fillAttr == null || fillAttr == "none") return defaultFill;

        // Check for CSS class-based fills
        var classAttr = element.Attribute("class")?.Value;
        if (classAttr != null)
        {
            // For cls-1 or st0 type classes, use default fill (they'll be themed)
            return defaultFill;
        }

        // Check for explicit color fills
        if (fillAttr.StartsWith("#"))
        {
            // Use default fill for common dark fills (we want to theme them)
            var lower = fillAttr.ToLowerInvariant();
            if (lower == "#000000" || lower == "#000" || lower == "#212121" || lower == "#444"
                || lower == "#444444" || lower == "#101010" || lower == "#12c8fd")
            {
                return defaultFill;
            }

            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(fillAttr);
                return new SolidColorBrush(color);
            }
            catch
            {
                return defaultFill;
            }
        }

        return defaultFill;
    }

    private static Brush? GetStrokeBrush(XElement element)
    {
        var strokeAttr = element.Attribute("stroke")?.Value;
        if (strokeAttr == null || strokeAttr == "none") return null;

        // Theme dark strokes to use the default icon color
        var lower = strokeAttr.ToLowerInvariant();
        if (lower == "#000000" || lower == "#000" || lower == "#212121")
        {
            return new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1));
        }

        try
        {
            var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(strokeAttr);
            return new SolidColorBrush(color);
        }
        catch
        {
            return null;
        }
    }

    private static double GetStrokeThickness(XElement element)
    {
        var strokeWidth = element.Attribute("stroke-width")?.Value;
        if (strokeWidth == null) return 1.0;
        return ParseDouble(strokeWidth, 1.0);
    }

    private static Transform? ParseTransform(string transformStr)
    {
        if (transformStr.StartsWith("translate("))
        {
            var inner = transformStr.Replace("translate(", "").TrimEnd(')');
            var parts = inner.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var tx = ParseDouble(parts[0], 0);
                var ty = ParseDouble(parts[1], 0);
                return new TranslateTransform(tx, ty);
            }
        }
        return null;
    }

    private static Geometry? ParsePolygonPoints(string pointsStr)
    {
        var parts = pointsStr.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return null;

        var points = new List<Point>();
        for (int i = 0; i < parts.Length - 1; i += 2)
        {
            points.Add(new Point(ParseDouble(parts[i], 0), ParseDouble(parts[i + 1], 0)));
        }

        if (points.Count < 2) return null;

        var figure = new PathFigure(points[0], points.Skip(1).Select(p => new LineSegment(p, true)), true);
        return new PathGeometry(new[] { figure });
    }

    private static double ParseDouble(string? value, double defaultValue)
    {
        if (value == null) return defaultValue;
        return double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Clears the icon cache (call when theme changes to regenerate icons with new colors).
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}
