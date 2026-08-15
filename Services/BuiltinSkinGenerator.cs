using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DeskPet.Services;

/// <summary>
/// Generates the built-in pet skins (cat/dog/rabbit/panda) so DeskPet works
/// out of the box. Users can replace any model with a sequence-frame skin
/// following PET.md.
/// </summary>
public static class BuiltinSkinGenerator
{
    public const string BuiltinDirName = "builtin";
    public static readonly string[] ModelNames = { "cat", "dog", "rabbit", "panda" };

    public static string ModelsRoot => Path.Combine(PetSkin.DefaultSkinDirectory, BuiltinDirName);

    private const int Size = 256;

    private enum Kind { Cat, Dog, Rabbit, Panda }

    private readonly record struct Spec(Color Body, Color Dark, Color Belly, Color EarInner, Kind Kind);

    public static void GenerateAll()
    {
        foreach (var name in ModelNames) GenerateModel(name);
    }

    private static void GenerateModel(string name)
    {
        var spec = name switch
        {
            "dog" => new Spec(Color.FromArgb(255, 160, 110, 60), Color.FromArgb(255, 130, 85, 40), Color.FromArgb(255, 235, 210, 180), Color.FromArgb(255, 120, 70, 40), Kind.Dog),
            "rabbit" => new Spec(Color.FromArgb(255, 230, 230, 235), Color.FromArgb(255, 190, 190, 200), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 250, 190, 200), Kind.Rabbit),
            "panda" => new Spec(Color.FromArgb(255, 245, 245, 245), Color.FromArgb(255, 40, 40, 40), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 40, 40, 40), Kind.Panda),
            _ => new Spec(Color.FromArgb(255, 240, 150, 60), Color.FromArgb(255, 220, 120, 40), Color.FromArgb(255, 255, 205, 140), Color.FromArgb(255, 255, 180, 150), Kind.Cat),
        };

        var dir = Path.Combine(ModelsRoot, name);
        WriteFrames(spec, Path.Combine(dir, "idle_0"), Idle);
        WriteFrames(spec, Path.Combine(dir, "walk_0"), Walk);
        WriteFrames(spec, Path.Combine(dir, "happy_0"), Happy);
        WriteFrames(spec, Path.Combine(dir, "hurt_0"), Hurt);
        WriteFrames(spec, Path.Combine(dir, "drag_0"), Drag);
        WriteFrames(spec, Path.Combine(dir, "fall_0"), Fall);
        WriteFrames(spec, Path.Combine(dir, "sleep_0"), Sleep);
        WriteFrames(spec, Path.Combine(dir, "yawn_0"), Yawn);
    }

    private static void WriteFrames(Spec spec, string folder, Pose[] poses)
    {
        Directory.CreateDirectory(folder);
        for (int i = 0; i < poses.Length; i++)
        {
            var path = Path.Combine(folder, $"{i + 1}.png");
            using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                DrawAnimal(g, poses[i], spec);
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    private readonly record struct Pose(
        float BodyX = 0, float BodyY = 0, float Stretch = 1f,
        float Rotation = 0f, bool EyesClosed = false, bool Happy = false,
        bool MouthOpen = false, float TailAngle = 0f);

    private static readonly Pose[] Idle =
    {
        new(0, 0, 1.00f),
        new(0, -2, 1.02f),
        new(0, 0, 1.00f),
        new(0, 1, 0.98f),
    };

    private static readonly Pose[] Walk =
    {
        new(-6, 2, 1f, -0.05f),
        new(0, -1, 1f, 0f),
        new(6, 2, 1f, 0.05f),
        new(0, -1, 1f, 0f),
    };

    private static readonly Pose[] Happy = { new(0, -3, 1f, 0f, Happy: true), new(0, -6, 1f, 0f, Happy: true) };
    private static readonly Pose[] Hurt = { new(0, 0, 0.95f, 0f, EyesClosed: true) };
    private static readonly Pose[] Drag = { new(0, 3, 1.1f, 0.12f, EyesClosed: true), new(0, 5, 1.1f, -0.10f, EyesClosed: true) };
    private static readonly Pose[] Fall = { new(0, 0, 1f, 0.35f, EyesClosed: true), new(0, 0, 1f, 0.65f, EyesClosed: true) };
    private static readonly Pose[] Sleep = { new(0, 6, 0.94f, 0f, EyesClosed: true) };
    private static readonly Pose[] Yawn = { new(0, 0, 1f, 0f, EyesClosed: true, MouthOpen: true) };

    private static void DrawAnimal(Graphics g, Pose p, Spec spec)
    {
        g.TranslateTransform(Size / 2f, Size / 2f);
        if (p.Rotation != 0) g.RotateTransform(p.Rotation);

        var body = new SolidBrush(spec.Body);
        var bodyDark = new SolidBrush(spec.Dark);
        var inner = new SolidBrush(spec.Belly);
        var earInner = new SolidBrush(spec.EarInner);
        var line = new Pen(spec.Kind == Kind.Panda ? spec.Dark : Color.FromArgb(220, 90, 60, 30), 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        float cx = p.BodyX;
        float cy = p.BodyY + 10;
        float bodyH = 84 * p.Stretch;
        float bodyW = 74 * p.Stretch;

        DrawTail(g, spec, cx, cy, bodyW, bodyH, p.TailAngle);

        // Body
        g.FillEllipse(body, cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);

        // Panda black limbs
        if (spec.Kind == Kind.Panda)
        {
            g.FillEllipse(bodyDark, cx - bodyW / 2 - 4, cy + bodyH / 2 - 18, 26, 22);
            g.FillEllipse(bodyDark, cx + bodyW / 2 - 22, cy + bodyH / 2 - 18, 26, 22);
        }

        g.FillEllipse(inner, cx - bodyW / 2 + 10, cy - bodyH / 2 + 16, bodyW - 20, bodyH - 30);

        // Head
        float headR = 40 * p.Stretch;
        float hx = cx;
        float hy = cy - bodyH / 2 - headR * 0.35f;
        g.FillEllipse(body, hx - headR, hy - headR, headR * 2, headR * 2);

        DrawEars(g, spec, hx, hy, headR);

        // Dog muzzle
        if (spec.Kind == Kind.Dog)
        {
            g.FillEllipse(body, hx - headR * 0.5f, hy - headR * 0.1f, headR, headR * 0.9f);
        }

        DrawEyes(g, p, hx, hy, headR, line);

        // Nose + mouth
        var nose = new SolidBrush(spec.Kind == Kind.Dog ? Color.FromArgb(255, 60, 40, 30) : Color.FromArgb(255, 235, 110, 130));
        g.FillEllipse(nose, hx - 5, hy + headR * 0.32f, 10, 7);
        if (p.MouthOpen)
        {
            g.FillEllipse(bodyDark, hx - 8, hy + headR * 0.5f, 16, 16);
        }
        else
        {
            g.DrawArc(line, hx - 8, hy + headR * 0.35f, 8, 8, 20, 140);
            g.DrawArc(line, hx, hy + headR * 0.35f, 8, 8, 20, 140);
        }

        // Whiskers (cat only)
        if (spec.Kind == Kind.Cat)
        {
            g.DrawLine(line, hx - headR * 0.6f, hy + headR * 0.25f, hx - headR * 1.15f, hy + headR * 0.15f);
            g.DrawLine(line, hx - headR * 0.6f, hy + headR * 0.4f, hx - headR * 1.15f, hy + headR * 0.45f);
            g.DrawLine(line, hx + headR * 0.6f, hy + headR * 0.25f, hx + headR * 1.15f, hy + headR * 0.15f);
            g.DrawLine(line, hx + headR * 0.6f, hy + headR * 0.4f, hx + headR * 1.15f, hy + headR * 0.45f);
        }
    }

    private static void DrawTail(Graphics g, Spec spec, float cx, float cy, float bodyW, float bodyH, float tailAngle)
    {
        var tailPen = new Pen(new SolidBrush(spec.Dark), 14f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var tailPath = new GraphicsPath();

        switch (spec.Kind)
        {
            case Kind.Rabbit:
                g.FillEllipse(new SolidBrush(spec.Belly), cx + bodyW / 2 - 8, cy + bodyH / 2 - 10, 20, 20);
                break;
            case Kind.Panda:
                g.FillEllipse(new SolidBrush(spec.Dark), cx - bodyW / 2 - 8, cy + bodyH / 2 - 14, 18, 18);
                break;
            case Kind.Dog:
                tailPath.AddBezier(
                    cx + bodyW / 2 - 6, cy - bodyH / 2 + 6,
                    cx + bodyW / 2 + 30, cy - bodyH / 2 - 6 + tailAngle * 6,
                    cx + bodyW / 2 + 24, cy - bodyH / 2 - 22,
                    cx + bodyW / 2 + 34, cy - bodyH / 2 - 34);
                g.DrawPath(tailPen, tailPath);
                break;
            default: // cat
                tailPath.AddBezier(
                    cx + bodyW / 2 - 4, cy + bodyH / 2 - 6,
                    cx + bodyW / 2 + 34, cy + bodyH / 2 - 14 + tailAngle * 6,
                    cx + bodyW / 2 + 26, cy - bodyH / 2 - 10,
                    cx + bodyW / 2 + 40, cy - bodyH / 2 - 30);
                g.DrawPath(tailPen, tailPath);
                break;
        }
    }

    private static void DrawEars(Graphics g, Spec spec, float hx, float hy, float headR)
    {
        var body = new SolidBrush(spec.Body);
        var earInner = new SolidBrush(spec.EarInner);

        switch (spec.Kind)
        {
            case Kind.Rabbit:
                g.FillEllipse(body, hx - headR * 0.9f, hy - headR * 2.0f, headR * 0.5f, headR * 1.4f);
                g.FillEllipse(body, hx + headR * 0.4f, hy - headR * 2.0f, headR * 0.5f, headR * 1.4f);
                g.FillEllipse(earInner, hx - headR * 0.82f, hy - headR * 1.8f, headR * 0.32f, headR * 1.0f);
                g.FillEllipse(earInner, hx + headR * 0.48f, hy - headR * 1.8f, headR * 0.32f, headR * 1.0f);
                break;

            case Kind.Dog:
                g.FillEllipse(body, hx - headR * 1.05f, hy - headR * 0.6f, headR * 0.7f, headR * 0.5f);
                g.FillEllipse(body, hx + headR * 0.35f, hy - headR * 0.6f, headR * 0.7f, headR * 0.5f);
                break;

            case Kind.Panda:
                g.FillEllipse(new SolidBrush(spec.Dark), hx - headR * 1.0f, hy - headR * 1.2f, headR * 0.55f, headR * 0.55f);
                g.FillEllipse(new SolidBrush(spec.Dark), hx + headR * 0.45f, hy - headR * 1.2f, headR * 0.55f, headR * 0.55f);
                break;

            default: // cat
                var ear = new PointF[]
                {
                    new(hx - headR + 4, hy - headR * 0.5f),
                    new(hx - headR * 0.9f, hy - headR * 1.5f),
                    new(hx - headR * 0.1f, hy - headR * 0.9f),
                };
                g.FillPolygon(body, ear);
                var ear2 = new PointF[]
                {
                    new(hx + headR - 4, hy - headR * 0.5f),
                    new(hx + headR * 0.9f, hy - headR * 1.5f),
                    new(hx + headR * 0.1f, hy - headR * 0.9f),
                };
                g.FillPolygon(body, ear2);
                var ie = new PointF[]
                {
                    new(hx - headR + 10, hy - headR * 0.45f),
                    new(hx - headR * 0.75f, hy - headR * 1.25f),
                    new(hx - headR * 0.18f, hy - headR * 0.8f),
                };
                g.FillPolygon(earInner, ie);
                var ie2 = new PointF[]
                {
                    new(hx + headR - 10, hy - headR * 0.45f),
                    new(hx + headR * 0.75f, hy - headR * 1.25f),
                    new(hx + headR * 0.18f, hy - headR * 0.8f),
                };
                g.FillPolygon(earInner, ie2);
                break;
        }
    }

    private static void DrawEyes(Graphics g, Pose p, float hx, float hy, float headR, Pen line)
    {
        if (p.Happy)
        {
            g.DrawArc(line, hx - headR * 0.55f, hy - headR * 0.25f, headR * 0.5f, headR * 0.5f, 200, 140);
            g.DrawArc(line, hx + headR * 0.05f, hy - headR * 0.25f, headR * 0.5f, headR * 0.5f, 200, 140);
        }
        else if (p.EyesClosed)
        {
            g.DrawLine(line, hx - headR * 0.55f, hy + headR * 0.05f, hx - headR * 0.15f, hy + headR * 0.05f);
            g.DrawLine(line, hx + headR * 0.15f, hy + headR * 0.05f, hx + headR * 0.55f, hy + headR * 0.05f);
        }
        else
        {
            // Panda eye patches
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, 40, 40, 40)), hx - headR * 0.52f, hy - headR * 0.12f, headR * 0.34f, headR * 0.4f);
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, 40, 40, 40)), hx + headR * 0.18f, hy - headR * 0.12f, headR * 0.34f, headR * 0.4f);
            var eye = new SolidBrush(Color.FromArgb(255, 30, 30, 20));
            g.FillEllipse(eye, hx - headR * 0.45f, hy - headR * 0.05f, headR * 0.18f, headR * 0.24f);
            g.FillEllipse(eye, hx + headR * 0.25f, hy - headR * 0.05f, headR * 0.18f, headR * 0.24f);
        }
    }
}
