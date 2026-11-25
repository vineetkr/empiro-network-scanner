using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Empiro.Domain;
namespace Empiro.App;
/// <summary>Retained drawing surface with interactive layout and explicit evidence links.</summary>
public sealed class NetworkMapControl : FrameworkElement
{
    public static readonly DependencyProperty AssetsProperty = DependencyProperty.Register(nameof(Assets), typeof(IEnumerable), typeof(NetworkMapControl), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty EdgesProperty = DependencyProperty.Register(nameof(Edges), typeof(IEnumerable), typeof(NetworkMapControl), new FrameworkPropertyMetadata(null, Changed));
    public IEnumerable? Assets { get => (IEnumerable?)GetValue(AssetsProperty); set => SetValue(AssetsProperty, value); }
    public IEnumerable? Edges { get => (IEnumerable?)GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }
    private readonly Dictionary<string, Point> positions = [];
    private string? dragging;
    private Point last;
    private Vector pan;
    private double zoom = 1;
    public NetworkMapControl()
    {
        ClipToBounds = true; Focusable = true;
        MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += Up;
        MouseWheel += (_, e) => { Zoom(e.Delta > 0 ? 1.1 : 0.9); e.Handled = true; };
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var map = (NetworkMapControl)d;
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= map.CollectionChanged;
        if (e.NewValue is INotifyCollectionChanged next) next.CollectionChanged += map.CollectionChanged;
        map.InvalidateVisual();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (e.Action == NotifyCollectionChangedAction.Reset) positions.Clear(); InvalidateVisual(); }
    public void Zoom(double factor) { zoom = Math.Clamp(zoom * factor, .35, 2.5); InvalidateVisual(); }
    public void ResetLayout() { positions.Clear(); pan = new(); zoom = 1; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Brush Brush(string name) => (Brush)FindResource(name);
        var background = Brush("CardBrush"); var border = Brush("BorderBrush"); var foreground = Brush("TextBrush"); var muted = Brush("MutedBrush");
        dc.DrawRoundedRectangle(background, new Pen(border, 1), new Rect(0, 0, ActualWidth, ActualHeight), 12, 12);
        for (var x = 20d; x < ActualWidth; x += 28) for (var y = 20d; y < ActualHeight; y += 28) dc.DrawEllipse(border, null, new Point(x, y), .7, .7);
        var assets = Assets?.Cast<Asset>().ToArray() ?? [];
        var edges = Edges?.Cast<TopologyEdge>().ToArray() ?? [];
        Text(dc, assets.Length == 0 ? "Scan your network to add devices to the map." : "Drag a node to arrange · drag the background to pan · scroll to zoom", new(20, 17), 11, muted);
        if (assets.Length == 0) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var hubIp = edges.GroupBy(e => e.Destination).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
        var satellites = assets.Where(a => a.Ip != hubIp).ToArray();
        if (hubIp != null && !positions.ContainsKey(hubIp)) positions[hubIp] = center;
        for (var i = 0; i < satellites.Length; i++)
        {
            var angle = i * Math.PI * 2 / satellites.Length - Math.PI / 2;
            if (!positions.ContainsKey(satellites[i].Ip)) positions[satellites[i].Ip] = new(center.X + Math.Cos(angle) * Math.Max(100, ActualWidth / 2 - 135), center.Y + Math.Sin(angle) * Math.Max(100, ActualHeight / 2 - 90));
        }
        dc.PushTransform(new TranslateTransform(pan.X, pan.Y)); dc.PushTransform(new ScaleTransform(zoom, zoom, center.X, center.Y));
        foreach (var edge in edges)
        {
            if (!positions.TryGetValue(edge.Source, out var from) || !positions.TryGetValue(edge.Destination, out var to)) continue;
            dc.DrawLine(new Pen(Brush("AccentBrush"), 1.2), from, to);
        }
        foreach (var asset in assets)
        {
            if (!positions.TryGetValue(asset.Ip, out var p)) continue;
            var rect = new Rect(p.X - 88, p.Y - 34, 176, 68);
            dc.DrawRoundedRectangle(Brush("SidebarBrush"), new Pen(border, 1), rect, 9, 9);
            dc.DrawEllipse(asset.Online ? Brush("GreenBrush") : Brush("AmberBrush"), null, new Point(rect.X + 13, rect.Y + 17), 3, 3);
            Text(dc, asset.DisplayName, new(rect.X + 24, rect.Y + 8), 11, foreground, 145);
            Text(dc, asset.Ip, new(rect.X + 13, rect.Y + 28), 11, muted);
            Text(dc, asset.Kind + (asset.Online ? " · Online" : " · Offline"), new(rect.X + 13, rect.Y + 45), 9, muted);
        }
        dc.Pop(); dc.Pop();
    }
    private void Text(DrawingContext dc, string value, Point point, double size, Brush brush, double width = 1000)
    {
        var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(1, width), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(text, point);
    }
    private Point World(Point p) { var center = new Point(ActualWidth / 2, ActualHeight / 2); return new((p.X - pan.X - center.X) / zoom + center.X, (p.Y - pan.Y - center.Y) / zoom + center.Y); }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        last = e.GetPosition(this); var p = World(last);
        dragging = positions.FirstOrDefault(n => new Rect(n.Value.X - 88, n.Value.Y - 34, 176, 68).Contains(p)).Key;
        CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return; var next = e.GetPosition(this);
        if (dragging != null) positions[dragging] += (next - last) / zoom; else pan += next - last;
        last = next; InvalidateVisual();
    }
    private void Up(object sender, MouseButtonEventArgs e) { ReleaseMouseCapture(); dragging = null; }
}
