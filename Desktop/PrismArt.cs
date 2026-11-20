using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace GameForge.Desktop
{
    internal static class PrismArt
    {
        private static readonly GeometryModel3D Glass = MakeGlass();

        private static GeometryModel3D MakeGlass()
        {
            const int around = 96, across = 28;
            var mesh = new MeshGeometry3D();
            for (int i = 0; i <= around; i++)
            {
                double u = 2 * Math.PI * i / around;
                double radius = .81 + .12 * Math.Cos(3 * u);
                double tube = .24 + .055 * Math.Sin(2 * u);
                for (int j = 0; j <= across; j++)
                {
                    double v = 2 * Math.PI * j / across;
                    double r = radius + tube * Math.Cos(v);
                    mesh.Positions.Add(new Point3D(r * Math.Cos(u), r * Math.Sin(u),
                        .28 * Math.Sin(2 * u + .55) + tube * Math.Sin(v)));
                    mesh.TextureCoordinates.Add(new Point(.5 + .48 * Math.Cos(u + .7 * Math.Sin(v)), .5));
                    if (i == around || j == across) continue;
                    int a = i * (across + 1) + j, b = a + across + 1;
                    foreach (int index in new[] { a, b, a + 1, a + 1, b, b + 1 }) mesh.TriangleIndices.Add(index);
                }
            }
            var film = new LinearGradientBrush { StartPoint = new Point(0,.5), EndPoint = new Point(1,.5) };
            film.GradientStops.Add(new GradientStop(WorldArt.ColorOf("#655BB4"), 0));
            film.GradientStops.Add(new GradientStop(WorldArt.ColorOf("#75A3DF"), .32));
            film.GradientStops.Add(new GradientStop(WorldArt.ColorOf("#64D7BC"), .67));
            film.GradientStops.Add(new GradientStop(WorldArt.ColorOf("#CAECD2"), 1));
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(film));
            material.Children.Add(new SpecularMaterial(Brushes.White, 38));
            var model = new GeometryModel3D(mesh, material);
            model.Freeze();
            return model;
        }

        internal static Grid Panel(double height, bool animate, bool thumbnail = false)
        {
            var panel = new Grid { Height = height, ClipToBounds = true, IsHitTestVisible = false,
                Background = new LinearGradientBrush(WorldArt.ColorOf("#171C2C"), WorldArt.ColorOf("#344F60"), 12) };
            var scene = new Model3DGroup();
            var pose = new Transform3DGroup();
            pose.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,0,1), -28)));
            pose.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0), 42)));
            var tilt = new AxisAngleRotation3D(new Vector3D(0,1,0), -9);
            pose.Children.Add(new RotateTransform3D(tilt));
            pose.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,0,1), 180)));
            var sculpture = new Model3DGroup { Transform = pose };
            sculpture.Children.Add(Glass);
            scene.Children.Add(sculpture);
            scene.Children.Add(new AmbientLight(WorldArt.ColorOf("#667284")));
            scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(.4,-.9,-1)));
            scene.Children.Add(new DirectionalLight(WorldArt.ColorOf("#C9E8FA"), new Vector3D(-1,.35,-.65)));
            var view = new Viewport3D { MaxWidth = height * 1.8, Camera = new OrthographicCamera(new Point3D(0,0,4),
                new Vector3D(0,0,-1), new Vector3D(0,1,0), thumbnail ? 3 : 3.65) };
            view.Children.Add(new ModelVisual3D { Content = scene });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.52, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.48, GridUnitType.Star) });
            if(thumbnail) Grid.SetColumnSpan(view, 2);
            else Grid.SetColumn(view, 1);
            panel.Children.Add(view);
            if (animate)
            {
                var motion = new DoubleAnimation(-9, 10, TimeSpan.FromSeconds(8)) { AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                view.Loaded += (s,e) => tilt.BeginAnimation(AxisAngleRotation3D.AngleProperty, motion);
                view.Unloaded += (s,e) => tilt.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
            }
            return panel;
        }
    }
}
