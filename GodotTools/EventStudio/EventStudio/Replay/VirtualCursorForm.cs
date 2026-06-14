using System.Drawing.Drawing2D;

namespace EventStudio.Replay;

internal sealed class VirtualCursorForm : Form
{
    private const int SurfaceSize = 240;
    private const int HotspotX = 92;
    private const int HotspotY = 84;
    private const int TrailCapacity = 18;

    private readonly List<TrailPoint> _trail = [];
    private readonly System.Windows.Forms.Timer _animationTimer;
    private bool _pressed;
    private float _glowPhase;
    private float _pulseStrength;
    private Point _cursorScreenPoint;

    internal VirtualCursorForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Width = SurfaceSize;
        Height = SurfaceSize;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) =>
        {
            _glowPhase += 0.18f;
            if (_glowPhase > MathF.PI * 2f)
            {
                _glowPhase -= MathF.PI * 2f;
            }

            for (var i = _trail.Count - 1; i >= 0; i--)
            {
                var point = _trail[i];
                point.Life -= 0.08f;
                if (point.Life <= 0f)
                {
                    _trail.RemoveAt(i);
                    continue;
                }

                _trail[i] = point;
            }

            if (_pulseStrength > 0f)
            {
                _pulseStrength = Math.Max(0f, _pulseStrength - 0.07f);
            }

            if (Visible)
            {
                Invalidate();
            }
        };
        _animationTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    internal void MoveToCursorPoint(Point screenPoint)
    {
        _cursorScreenPoint = screenPoint;
        _trail.Add(new TrailPoint(screenPoint, 1f));
        while (_trail.Count > TrailCapacity)
        {
            _trail.RemoveAt(0);
        }

        Location = new Point(screenPoint.X - HotspotX, screenPoint.Y - HotspotY);
        Invalidate();
    }

    internal async Task PulseAsync()
    {
        _pressed = true;
        _pulseStrength = 1f;
        Invalidate();
        await Task.Delay(140);
        _pressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        var cursorPoint = ToLocal(_cursorScreenPoint);
        DrawTrail(e.Graphics);
        DrawGlow(e.Graphics, cursorPoint);
        DrawCursor(e.Graphics, cursorPoint);
        DrawPulse(e.Graphics, cursorPoint);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawTrail(Graphics graphics)
    {
        if (_trail.Count < 2)
        {
            return;
        }

        for (var i = 1; i < _trail.Count; i++)
        {
            var from = _trail[i - 1];
            var to = _trail[i];
            var fromPoint = ToLocal(from.ScreenPoint);
            var toPoint = ToLocal(to.ScreenPoint);
            var blend = Math.Clamp((from.Life + to.Life) * 0.5f, 0f, 1f);
            var outerAlpha = (int)(110 * blend);
            var innerAlpha = (int)(220 * blend);
            var width = 6f + 12f * blend;
            using var outerPen = new Pen(Color.FromArgb(outerAlpha, 48, 208, 255), width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var innerPen = new Pen(Color.FromArgb(innerAlpha, 255, 240, 120), Math.Max(2.5f, width * 0.34f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(outerPen, fromPoint, toPoint);
            graphics.DrawLine(innerPen, fromPoint, toPoint);
        }
    }

    private void DrawGlow(Graphics graphics, Point cursorPoint)
    {
        var glowScale = 0.92f + 0.08f * (MathF.Sin(_glowPhase) + 1f);
        var outerSize = 70f * glowScale;
        var middleSize = 46f * glowScale;
        var coreSize = 24f * glowScale;
        FillCenteredEllipse(graphics, cursorPoint, outerSize, Color.FromArgb(_pressed ? 84 : 60, 32, 224, 255));
        FillCenteredEllipse(graphics, cursorPoint, middleSize, Color.FromArgb(_pressed ? 118 : 90, 255, 206, 64));
        FillCenteredEllipse(graphics, cursorPoint, coreSize, Color.FromArgb(_pressed ? 160 : 120, 255, 248, 200));
    }

    private void DrawCursor(Graphics graphics, Point cursorPoint)
    {
        var shadow =
            new[]
            {
                new PointF(0, 0),
                new PointF(0, 54),
                new PointF(14, 40),
                new PointF(24, 63),
                new PointF(35, 58),
                new PointF(25, 34),
                new PointF(48, 34)
            };

        var arrow =
            new[]
            {
                new PointF(-3, -3),
                new PointF(-3, 51),
                new PointF(11, 37),
                new PointF(21, 60),
                new PointF(32, 55),
                new PointF(22, 31),
                new PointF(45, 31)
            };

        using var shadowBrush = new SolidBrush(Color.FromArgb(112, 0, 0, 0));
        using var arrowGlowPen = new Pen(Color.FromArgb(180, 72, 220, 255), 8)
        {
            LineJoin = LineJoin.Round
        };
        using var arrowBrush = new SolidBrush(Color.White);
        using var outlinePen = new Pen(Color.FromArgb(24, 24, 24), 2.2f)
        {
            LineJoin = LineJoin.Round
        };
        var shadowState = graphics.Save();
        graphics.TranslateTransform(cursorPoint.X - 16, cursorPoint.Y - 14);
        graphics.FillPolygon(shadowBrush, shadow);
        graphics.Restore(shadowState);
        var cursorState = graphics.Save();
        graphics.TranslateTransform(cursorPoint.X - 18, cursorPoint.Y - 16);
        graphics.DrawPolygon(arrowGlowPen, arrow);
        graphics.FillPolygon(arrowBrush, arrow);
        graphics.DrawPolygon(outlinePen, arrow);
        graphics.Restore(cursorState);
    }

    private void DrawPulse(Graphics graphics, Point cursorPoint)
    {
        if (_pulseStrength <= 0f && !_pressed)
        {
            return;
        }

        var strength = Math.Max(_pulseStrength, _pressed ? 0.75f : 0f);
        var radius = 24f + (1f - strength) * 26f;
        var alpha = (int)(220 * strength);
        using var ringPen = new Pen(Color.FromArgb(alpha, 255, 144, 96), 4f + strength * 2f);
        graphics.DrawEllipse(
            ringPen,
            cursorPoint.X - radius,
            cursorPoint.Y - radius,
            radius * 2f,
            radius * 2f);
    }

    private Point ToLocal(Point screenPoint)
    {
        return new Point(screenPoint.X - Location.X, screenPoint.Y - Location.Y);
    }

    private static void FillCenteredEllipse(Graphics graphics, Point center, float size, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, center.X - size / 2f, center.Y - size / 2f, size, size);
    }

    private sealed class TrailPoint(Point screenPoint, float life)
    {
        internal Point ScreenPoint { get; } = screenPoint;
        internal float Life { get; set; } = life;
    }
}
