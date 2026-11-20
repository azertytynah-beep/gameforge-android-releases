using System;
using System.Windows;
using System.Windows.Media;

namespace GameForge.Desktop
{
    // Illustrations vectorielles originales. Le mouvement déplace le décor entier sur la carte.
    internal sealed class WorldArt : FrameworkElement
    {
        private readonly bool relief, light;
        internal WorldArt(bool relief, bool light) { this.relief = relief; this.light = light; ClipToBounds = true; IsHitTestVisible = false; }
        internal static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        internal static SolidColorBrush Ink(string hex) => new SolidColorBrush(ColorOf(hex));
        private static LinearGradientBrush Wash(string top, string bottom) => new LinearGradientBrush(ColorOf(top), ColorOf(bottom), 90);
        private static Pen Line(string color, double width = 1) => new Pen(Ink(color), width);
        private static StreamGeometry Shape(Point[] points)
        {
            var result = new StreamGeometry();
            using (var ctx = result.Open())
            {
                ctx.BeginFigure(points[0], true, true);
                for (int i = 1; i < points.Length; i++) ctx.LineTo(points[i], true, false);
            }
            result.Freeze();
            return result;
        }
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
            double scale = Math.Max(w / 960, h / 330);
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, w - 960 * scale, (h - 330 * scale) / 2));
            if (relief) Landscape(dc); else Orbit(dc);
            dc.Pop();
            dc.Pop();
        }

        private void Orbit(DrawingContext d)
        {
            d.DrawRectangle(Wash(light ? "#4C4888" : "#16132F", light ? "#4D709C" : "#2C234F"), null, new Rect(0, 0, 960, 330));
            var halo = new RadialGradientBrush(ColorOf("#B6B5A0FE"), ColorOf("#00271A63"));
            d.DrawEllipse(halo, null, new Point(734, 146), 285, 230);
            var rng = new Random(41);
            for (int i = 0; i < 175; i++)
            {
                double x = rng.NextDouble() * 960, y = rng.NextDouble() * 330;
                double r = .28 + rng.NextDouble() * .85;
                d.DrawEllipse(Ink(i % 5 == 0 ? "#BFEEE3FF" : "#73AFBDDA"), null, new Point(x,y), r,r);
            }
            var center = new Point(747, 149);
            d.PushTransform(new RotateTransform(-18, center.X, center.Y));
            for (int i = 0; i < 3; i++)
                d.DrawEllipse(null,Line("#427F83BE", .8), center, 219 + i*47, 74 + i*21);
            var planet = new RadialGradientBrush();
            planet.GradientOrigin = new Point(.27,.18);
            planet.Center = new Point(.38,.22);
            planet.RadiusX = .79; planet.RadiusY = .82;
            planet.GradientStops.Add(new GradientStop(ColorOf("#EAE6FF"), 0));
            planet.GradientStops.Add(new GradientStop(ColorOf("#AF9EF0"), .35));
            planet.GradientStops.Add(new GradientStop(ColorOf("#6559AD"), .64));
            planet.GradientStops.Add(new GradientStop(ColorOf("#282347"), .86));
            planet.GradientStops.Add(new GradientStop(ColorOf("#16152D"), 1));
            d.DrawEllipse(planet, Line("#AAA8A8DF", 1.1), center, 92, 92);
            d.PushClip(new EllipseGeometry(center,92,92));
            for (int i = 0; i < 47; i++)
            {
                var swirl = new StreamGeometry();
                using (var c = swirl.Open())
                {
                    double y=61+i*3.9;
                    c.BeginFigure(new Point(640,y),false,false);
                    c.BezierTo(new Point(715,y+37+8*Math.Sin(i*.21)),new Point(786,y-25),new Point(852,y-3),true,false);
                }
                d.DrawGeometry(null,Line(i%4==0 ? "#376B62A9" : "#26D8D6FF", .55+i%3*.45),swirl);
            }
            d.Pop();
            for(int i=0;i<10;i++)
            {
                double rx=154+i*2.7, ry=25+i*.85;
                var arc=new StreamGeometry();
                using(var c=arc.Open()){
                    c.BeginFigure(new Point(center.X-rx,center.Y),false,false);
                    c.ArcTo(new Point(center.X+rx,center.Y),new Size(rx,ry),0,false,SweepDirection.Counterclockwise,true,false);
                }
                d.DrawGeometry(null,Line(i==3 ? "#FFE8DEFF" : "#88CDBAF6", i%3==0 ? 1.8 : .7),arc);
            }
            d.DrawEllipse(Ink("#F4DCCA"),null,new Point(530,149),3.2,3.2);
            d.Pop();
            foreach (var p in new[]{new Point(585,65),new Point(890,205),new Point(504,198)})
            {
                d.DrawLine(Line("#B9D2D1FF"),new Point(p.X-3,p.Y),new Point(p.X+3,p.Y));
                d.DrawLine(Line("#B9D2D1FF"),new Point(p.X,p.Y-3),new Point(p.X,p.Y+3));
            }
        }

        private void Landscape(DrawingContext d)
        {
            d.DrawRectangle(Wash(light ? "#D5A78F" : "#9F717D","#F9C48D"),null,new Rect(0,0,960,330));
            d.DrawEllipse(new RadialGradientBrush(ColorOf("#FFFCE7BC"),ColorOf("#00FECE96")),null,new Point(763,108),136,136);
            d.DrawEllipse(Ink("#FFF2CD"),null,new Point(763,108),46,46);
            var random=new Random(19);
            for (int i=0;i<3400;i++)
            {
                var p=new Point(random.NextDouble()*960, random.NextDouble()*330);
                d.DrawEllipse(Ink("#26774D51"),null,p,.55,.55);
            }
            string[] shades={"#C38B83","#AC757C","#805F76","#5D506C","#383C54"};
            for (int layer=0;layer<5;layer++)
            {
                var points=new Point[124];
                for(int i=0;i<122;i++)
                {
                    double x=i*8;
                    double y=144+layer*31+29*Math.Sin(x*.008+layer*1.72)+
                        13*Math.Sin(x*.025+layer)+7*Math.Cos(x*.043+layer*.6);
                    points[i]=new Point(x,y);
                }
                points[122]=new Point(968,345); points[123]=new Point(0,345);
                var shape=Shape(points);
                d.DrawGeometry(Ink(shades[layer]),null,shape);
                d.PushClip(shape);
                for(int j=1;j<=7;j++)
                {
                    var path=new StreamGeometry();
                    using(var c=path.Open()){
                        c.BeginFigure(new Point(0,points[0].Y+j*5),false,false);
                        for(int i=1;i<122;i++) c.LineTo(new Point(points[i].X,points[i].Y+j*5+Math.Sin(i*.13)*j*.5),true,false);
                    }
                    d.DrawGeometry(null,Line("#39503548",.8),path);
                }
                d.Pop();
            }
        }
    }
}
