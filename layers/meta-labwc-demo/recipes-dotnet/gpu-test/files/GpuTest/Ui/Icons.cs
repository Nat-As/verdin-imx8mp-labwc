// Vector icons of the navigation bar, drawn for this app on a 24x24 grid
// (no icon font). The gear is generated.
using System;
using Avalonia;
using Avalonia.Media;

namespace GpuTest.Ui
{
    internal static class Icons
    {
        // House with a door
        public static Geometry Home() =>
            Geometry.Parse("M12,2.6 L1.6,11.9 L3,13.4 L4.6,12 L4.6,21.4 L10,21.4 L10,15.4 L14,15.4 L14,21.4 " +
                           "L19.4,21.4 L19.4,12 L21,13.4 L22.4,11.9 Z");

        // Three bars
        public static Geometry Menu() =>
            Geometry.Parse("M3,5 L21,5 L21,7.6 L3,7.6 Z M3,10.7 L21,10.7 L21,13.3 L3,13.3 Z " +
                           "M3,16.4 L21,16.4 L21,19 L3,19 Z");

        // Open book: two curved pages
        public static Geometry Book() =>
            Geometry.Parse("M11.2,6.2 C8.8,4.4 5.2,4 1.8,4.8 L1.8,19.6 C5.2,18.8 8.6,19.2 11.2,20.8 Z " +
                           "M12.8,6.2 C15.2,4.4 18.8,4 22.2,4.8 L22.2,19.6 C18.8,18.8 15.4,19.2 12.8,20.8 Z");

        // Gear: 8 teeth around a ring with a hole (even-odd fill)
        public static Geometry Gear()
        {
            const int teeth = 8;
            const double cx = 12, cy = 12, outer = 10.6, inner = 8.2, hole = 3.4;
            var g = new StreamGeometry();
            using (StreamGeometryContext ctx = g.Open())
            {
                ctx.SetFillRule(FillRule.EvenOdd);
                double step = 2 * Math.PI / teeth;
                for (int i = 0; i < teeth; i++)
                {
                    double a = i * step - Math.PI / 2;
                    // tooth: flat top between a-0.22*step and a+0.22*step, sloped flanks
                    Point P(double angle, double r) => new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
                    Point p0 = P(a - 0.32 * step, inner), p1 = P(a - 0.2 * step, outer);
                    Point p2 = P(a + 0.2 * step, outer), p3 = P(a + 0.32 * step, inner);
                    if (i == 0)
                        ctx.BeginFigure(p0, true);
                    else
                        ctx.LineTo(p0);
                    ctx.LineTo(p1);
                    ctx.LineTo(p2);
                    ctx.LineTo(p3);
                    ctx.ArcTo(P(a + 0.68 * step, inner), new Size(inner, inner), 0, false, SweepDirection.Clockwise);
                }
                ctx.EndFigure(true);
                ctx.BeginFigure(new Point(cx + hole, cy), true);
                ctx.ArcTo(new Point(cx - hole, cy), new Size(hole, hole), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(cx + hole, cy), new Size(hole, hole), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
            return g;
        }

        // Keyboard key caps (for the keyboard button and the hide key)
        public static Geometry Keyboard() =>
            Geometry.Parse("M2,5 L22,5 L22,19 L2,19 Z M4,7 L4,17 L20,17 L20,7 Z " +
                           "M5.5,8.5 L7.5,8.5 L7.5,10.5 L5.5,10.5 Z M9,8.5 L11,8.5 L11,10.5 L9,10.5 Z " +
                           "M12.5,8.5 L14.5,8.5 L14.5,10.5 L12.5,10.5 Z M16,8.5 L18,8.5 L18,10.5 L16,10.5 Z " +
                           "M7,13.5 L17,13.5 L17,15.5 L7,15.5 Z");
    }
}
