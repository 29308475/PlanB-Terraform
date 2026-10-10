using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlanBProductionChains
{
    public sealed class PictogramPath
    {
        public readonly List<float[]> Points = new List<float[]>();
        public string Svg;
        public float Outline = 4.2f, Accent = 2.2f;
        public bool Filled, AccentFill;
    }

    // One 32-unit master supplies the SVG assets, design previews and Unity mesh.
    public static class PictogramGeometry
    {
        public static PictogramPath[] Chain()
        {
            return new[]
            {
                Curve(new float[][] {
                    new float[] { 15, 10 }, new float[] { 18, 7 },
                    new float[] { 20.2f, 4.8f, 23.8f, 4.8f, 26, 7 },
                    new float[] { 28.2f, 9.2f, 28.2f, 12.8f, 26, 15 }, new float[] { 23, 18 } }),
                Curve(new float[][] {
                    new float[] { 9, 14 }, new float[] { 6, 17 },
                    new float[] { 3.8f, 19.2f, 3.8f, 22.8f, 6, 25 },
                    new float[] { 8.2f, 27.2f, 11.8f, 27.2f, 14, 25 }, new float[] { 17, 22 } }),
                Curve(new float[][] { new float[] { 12, 20 }, new float[] { 20, 12 } })
            };
        }

        public static PictogramPath[] Rebuild()
        {
            var arc = new PictogramPath { Outline = 3.8f, Accent = 1.8f };
            var svg = new StringBuilder();
            for (var i = 0; i <= 112; i++)
            {
                var angle = (40 + i * 275.0 / 112) * Math.PI / 180;
                var x = (float)(16 + Math.Cos(angle) * 10);
                var y = (float)(16 + Math.Sin(angle) * 10);
                arc.Points.Add(new[] { x, y });
                svg.Append(i == 0 ? "M" : " L").Append(Number(x)).Append(' ').Append(Number(y));
            }
            arc.Points.Add(new float[] { 25, 11 }); svg.Append(" L25 11"); arc.Svg = svg.ToString();
            var arrow = Curve(new float[][] { new float[] { 19.5f, 11 }, new float[] { 25, 11 }, new float[] { 25, 5.5f } });
            arrow.Outline = 3.8f; arrow.Accent = 1.8f;
            return new[] { arc, arrow, Hexagon(4.4f, false), Hexagon(3.15f, true) };
        }

        private static PictogramPath Hexagon(float radius, bool accent)
        {
            var path = new PictogramPath { Filled = true, AccentFill = accent };
            var svg = new StringBuilder();
            for (var i = 0; i < 6; i++)
            {
                var angle = (30 + i * 60) * Math.PI / 180;
                var x = (float)(16 + Math.Cos(angle) * radius);
                var y = (float)(16 + Math.Sin(angle) * radius);
                path.Points.Add(new[] { x, y });
                svg.Append(i == 0 ? "M" : " L").Append(Number(x)).Append(' ').Append(Number(y));
            }
            path.Svg = svg.Append(" Z").ToString(); return path;
        }

        private static PictogramPath Curve(float[][] commands)
        {
            var path = new PictogramPath();
            var svg = new StringBuilder();
            var previous = commands[0]; path.Points.Add(previous);
            svg.Append("M").Append(Number(previous[0])).Append(' ').Append(Number(previous[1]));
            for (var i = 1; i < commands.Length; i++)
            {
                var command = commands[i];
                svg.Append(command.Length == 2 ? " L" : " C");
                foreach (var value in command) svg.Append(Number(value)).Append(' ');
                if (command.Length == 2) { path.Points.Add(command); previous = command; continue; }
                for (var sample = 1; sample <= 32; sample++)
                {
                    var t = sample / 32f; var u = 1 - t;
                    path.Points.Add(new[] {
                        u*u*u*previous[0] + 3*u*u*t*command[0] + 3*u*t*t*command[2] + t*t*t*command[4],
                        u*u*u*previous[1] + 3*u*u*t*command[1] + 3*u*t*t*command[3] + t*t*t*command[5] });
                }
                previous = new[] { command[4], command[5] };
            }
            path.Svg = svg.ToString(); return path;
        }

        private static string Number(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }

        public static string Svg(PictogramPath[] paths)
        {
            var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 32 32\" fill=\"none\">\n");
            foreach (var path in paths)
                if (!path.Filled) svg.Append("  <path d=\"").Append(path.Svg).Append("\" stroke=\"#000000\" stroke-width=\"")
                    .Append(Number(path.Outline)).Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
            foreach (var path in paths)
                if (!path.Filled) svg.Append("  <path d=\"").Append(path.Svg).Append("\" stroke=\"#797df5\" stroke-width=\"")
                    .Append(Number(path.Accent)).Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
            foreach (var path in paths)
                if (path.Filled) svg.Append("  <path d=\"").Append(path.Svg).Append("\" fill=\"")
                    .Append(path.AccentFill ? "#797df5" : "#000000").Append("\"/>\n");
            return svg.Append("</svg>\n").ToString();
        }
    }
}
