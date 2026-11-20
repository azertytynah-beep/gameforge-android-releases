using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameForge.Editor;

namespace GameForge.Desktop
{
    internal sealed partial class StudioWindow : Window
    {
        private readonly StudioRuntime studio;
        private ForgeProjectService Service => studio.Projects;
        private ForgeProject draft;
        private string design;
        private bool light, still, busy;
        private readonly bool proof;
        private string page = "Accueil";
        private StackPanel body, nav;
        private Grid shell;
        private Border pointerAura;
        private ScrollViewer contentScroll;
        private TextBlock notice;
        private SpeechRecognitionEngine mic;
        private readonly DispatcherTimer micTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        private CancellationTokenSource request;
        private const string Prism = "Prisme";
        private string Bg => light ? "#F4F3FA" : "#11141D";
        private string Surface => light ? "#FFFFFF" : "#1A2030";
        private string Edge => light ? "#D5D1DA" : "#363440";
        private string Fg => light ? "#211E2C" : "#FAF7FF";
        private string Muted => light ? "#595563" : "#B9B5C9";
        private string Accent => design == Prism ? (light ? "#4D3E91" : "#CEFFEA") :
            design == "Relief" ? (light ? "#935338" : "#F0B995") : (light ? "#5F4BA5" : "#BDAEFF");
        private static SolidColorBrush B(string v) => WorldArt.Ink(v);
        private static Thickness Gap(double top=0,double bottom=0) => new Thickness(0,top,0,bottom);
        private static string Stage(ForgeProject p) => new[]{"Idée de départ","Questionnaire","Apparence","Gameplay","Jeu créé"}[(int)p.stage];

        internal StudioWindow(StudioRuntime runtime, bool verification=false)
        {
            studio=runtime; proof=verification;
            string saved = proof ? Prism : Preferences.GetString("Design", Prism);
            design = saved == "Relief" || saved == "Orbital" ? saved : Prism;
            light=!proof && Preferences.GetString("Theme","Sombre")=="Clair";
            still=proof || Preferences.GetString("Motion","Oui")=="Non";
            Title="GameForge AI"; Width=1230; Height=810; MinWidth=720; MinHeight=520;
            WindowStartupLocation=WindowStartupLocation.CenterScreen; FontFamily=new FontFamily("Segoe UI");
            UseLayoutRounding=true;
            micTimer.Tick+=(s,e)=>StopVoice();
            Closing+=(s,e)=>{ if(busy && request==null){ e.Cancel=true; Status("Unity termine la scène. Garde le Studio ouvert jusqu’à la fin."); } else {request?.Cancel(); StopVoice();} };
            Draw();
        }
        private void Status(string value) { notice.Text=value; notice.Visibility=string.IsNullOrEmpty(value)?Visibility.Collapsed:Visibility.Visible; }
        private void Safe(Action action)
        {
            try { action(); } catch(Exception ex) { Status(ex.GetBaseException().Message); }
        }
        private TextBlock Text(string value,double size=15,string color=null, bool bold=false)
        {
            return new TextBlock{Text=value,FontSize=size,Foreground=B(color??Fg),TextWrapping=TextWrapping.Wrap,
                FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};
        }
        private Button Btn(string label,Action action,bool primary=false,bool flat=false)
        {
            var b=new Button{Content=label,Padding=new Thickness(15,9,15,10),FontSize=14,
                Foreground=B(primary ? (light?"#FFFFFF":"#201A28") : Fg),
                Background=B(primary?Accent:flat?"#00000000":Surface),BorderBrush=B(flat?"#00000000":primary?Accent:Edge),
                BorderThickness=new Thickness(1),HorizontalContentAlignment=HorizontalAlignment.Center,
                VerticalContentAlignment=VerticalAlignment.Center,Cursor=System.Windows.Input.Cursors.Hand,
                Template=(ControlTemplate)Resources["ButtonSkin"]};
            b.Click+=(s,e)=>Safe(action);
            b.MouseMove+=(s,e)=>MoveAura(e.GetPosition(shell));
            b.MouseLeave+=(s,e)=>MoveAura(null);
            return b;
        }
        private Border Card(UIElement child,double pad=18)
        {
            return new Border{Child=child,Background=B(Surface),BorderBrush=B(Edge),BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(14),Padding=new Thickness(pad)};
        }
        private void Go(string next){ if(busy) return; page=next; Draw(); }
        private void Draw()
        {
            StopVoice();
            Background=B(Bg);
            if(!Resources.Contains("ButtonSkin"))
                Resources["ButtonSkin"]=XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center' RecognizesAccessKey='True'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Opacity' Value='.82'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#B28AFF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='surface' Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            shell=new Grid{Background=B(Bg)};
            shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            shell.RowDefinitions.Add(new RowDefinition());
            var glow=new RadialGradientBrush(WorldArt.ColorOf(light?"#337D8ADA":"#404A396F"),Colors.Transparent)
                {Center=new Point(.2,0),GradientOrigin=new Point(.2,0),RadiusX=.85,RadiusY=.9};
            var ambient=new Border{Background=glow,IsHitTestVisible=false};
            if(!still && SystemParameters.ClientAreaAnimation)ambient.BeginAnimation(OpacityProperty,new DoubleAnimation(.55,1,TimeSpan.FromSeconds(9))
                {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
            Grid.SetRowSpan(ambient,2);shell.Children.Add(ambient);
            var bar=new DockPanel{Margin=new Thickness(24,16,24,16)};
            shell.Children.Add(bar);
            var brand=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            var mark=new Grid{Width=34,Height=34};
            mark.Children.Add(new Border{Background=B(Accent),CornerRadius=new CornerRadius(10)});
            mark.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M 8,23 L 14,9 L 27,9 L 24,14 L 17,14 L 16,17 L 22,17 L 20,21 L 14,21 L 13,24 Z"),Fill=B(light?"#FFFFFF":"#272139")});
            brand.Children.Add(mark); brand.Children.Add(new TextBlock{Text="GameForge AI",Foreground=B(Fg),FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(8,3,0,0)});
            bar.Children.Add(brand);
            var foot=Btn(light?"◐   Thème sombre":"☼   Thème clair",()=>{if(!busy){light=!light; SaveStyle(); Draw();}},false,true);
            foot.Padding=new Thickness(8,5,8,5);DockPanel.SetDock(foot,Dock.Right);bar.Children.Add(foot);
            nav=new StackPanel{Orientation=Orientation.Horizontal};
            bar.Children.Add(new Border{Child=nav,HorizontalAlignment=HorizontalAlignment.Center,
                Background=B(Surface),BorderBrush=B(Edge),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(3)});
            foreach(var destination in new[]{"Accueil","Mes projets","Ambiances","Réglages"}) Nav(destination);
            var right=new DockPanel{Margin=new Thickness(30,6,30,22)}; Grid.SetRow(right,1); shell.Children.Add(right);
            notice=Text("",14,Accent,true); notice.Margin=Gap(8); DockPanel.SetDock(notice,Dock.Bottom); right.Children.Add(notice); Status("");
            body=new StackPanel{MaxWidth=1240};
            contentScroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalOnly};
            AutomationProperties.SetName(contentScroll,"Contenu du Studio · défilement à la molette ou au clavier");
            right.Children.Add(contentScroll);
            Content=shell;
            if(page=="Accueil") Home();
            else if(page=="Ambiances") Styles();
            else if(page=="Mes projets") {Heading("Mes projets"); ListProjects(false); }
            else if(page=="Réglages") Settings();
            else Workshop();
            pointerAura=new Border{Width=180,Height=180,IsHitTestVisible=false,Visibility=Visibility.Collapsed,
                HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,
                Background=new RadialGradientBrush(WorldArt.ColorOf(light?"#184D3E91":"#24CEFFEA"),Colors.Transparent),
                RenderTransform=new TranslateTransform()};
            Grid.SetRowSpan(pointerAura,2);shell.Children.Add(pointerAura);
        }
        private void MoveAura(Point? point)
        {
            if(pointerAura==null)return;
            bool visible=point.HasValue && !still && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
            pointerAura.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            if(!visible)return;
            var move=(TranslateTransform)pointerAura.RenderTransform;
            move.X=point.Value.X-pointerAura.Width/2;move.Y=point.Value.Y-pointerAura.Height/2;
        }
        private void Nav(string dest)
        {
            var b=Btn(dest,()=>Go(dest),false,true);
            b.Padding=new Thickness(9,6,9,6);
            if(dest==page){ b.Background=B(light?"#DAD3E4":"#403A50"); b.FontWeight=FontWeights.SemiBold; }
            nav.Children.Add(b);
        }
        private void Heading(string label){var t=Text(label,31,Fg,true); t.Margin=Gap(2,15);body.Children.Add(t);}
        private void SaveStyle()
        {
            if(proof) return;
            Preferences.SetString("Design",design); Preferences.SetString("Theme",light?"Clair":"Sombre");
            Preferences.SetString("Motion",still?"Non":"Oui");
        }
        private Grid Illustration(string style,double height,bool animate)
        {
            var g=new Grid{Height=height,ClipToBounds=true};
            if(style==Prism){
                g.Children.Add(PrismArt.Panel(height,animate && !still && SystemParameters.ClientAreaAnimation));
                return g;
            }
            var art=new WorldArt(style=="Relief",light){Margin=new Thickness(-5),RenderTransform=new TranslateTransform()};
            g.Children.Add(art);
            if(animate && !still && SystemParameters.ClientAreaAnimation){
                var drift=new DoubleAnimation(-4,4,TimeSpan.FromSeconds(10)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,
                    EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
                art.RenderTransform.BeginAnimation(TranslateTransform.XProperty,drift);
                art.Unloaded+=(s,e)=>art.RenderTransform.BeginAnimation(TranslateTransform.XProperty,null);
            }
            return g;
        }
        private void Home()
        {
            bool warm = design=="Relief";
            var hero=Illustration(design,294,true);
            hero.Children.Add(new Border{Background=new LinearGradientBrush(
                new GradientStopCollection{new GradientStop(WorldArt.ColorOf(warm?"#F9382A36":"#ED171C2C"),0),
                new GradientStop(WorldArt.ColorOf(warm?"#ED42313D":"#BF1E293E"),.33),
                new GradientStop(WorldArt.ColorOf("#001E1838"),.69)},0)});
            var intro=new StackPanel{HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(26,0,8,0)};
            intro.Children.Add(Text("Atelier de création",14,"#E1D8EE",true));
            var h=Text("Nouveau jeu",52,"#FFF9FB",true);h.Margin=Gap(4,6);
            if(warm){h.FontFamily=new FontFamily("Georgia");h.FontStyle=FontStyles.Italic;h.FontWeight=FontWeights.Normal;}
            intro.Children.Add(h);
            var hint=Text("Décris ton idée pour commencer le brief.",15,"#CFD6EA");hint.Margin=Gap(0,16);intro.Children.Add(hint);
            var create=Btn("＋   Créer un jeu",Create,true);
            AutomationProperties.SetName(create,"Créer un jeu");
            create.HorizontalAlignment=HorizontalAlignment.Left;
            create.Background=B(warm?"#F5CBAB":design==Prism?"#CEFFEA":"#D7CCFF");create.Foreground=B("#152421");create.BorderThickness=new Thickness(0);
            intro.Children.Add(create);hero.Children.Add(intro);
            body.Children.Add(Framed(hero));
            Section("Projets récents","Tous les projets  →",()=>Go("Mes projets"));ListProjects(true);
        }
        private Border Framed(Grid image)
        {
            var b=new Border{Child=image,CornerRadius=new CornerRadius(14)};
            b.SizeChanged+=(s,e)=>b.Clip=new RectangleGeometry(new Rect(b.RenderSize),14,14);
            return b;
        }
        private void Section(string title,string action,Action click)
        {
            var row=new DockPanel{Margin=Gap(18,9)};var button=Btn(action,click,false,true);
            button.Padding=new Thickness(8,5,8,5);button.Foreground=B(Accent); DockPanel.SetDock(button,Dock.Right);row.Children.Add(button);
            var h=Text(title,21,Fg,true);row.Children.Add(h);body.Children.Add(row);
        }
        private void ListProjects(bool recent)
        {
            var drafts=Service.List();
            if(drafts.Count==0){
                var wrap=new StackPanel();wrap.Children.Add(Text("Ton premier jeu commence ici",17,Fg,true));
                wrap.Children.Add(Text("Tes projets apparaîtront ici dès leur création.",14,Muted));
                var card=Card(wrap,16);body.Children.Add(card);
                if(!recent)body.Children.Add(Btn("Commencer à créer un jeu",Create,true));return;
            }
            Panel list=body;
            if(recent){list=new UniformGrid{Columns=2};body.Children.Add(list);}
            foreach(var p in drafts.Take(recent?2:int.MaxValue)){
                var button=Btn("",()=>{draft=Service.Read(p.id);Go("Atelier");});
                var line=new DockPanel();var arrow=Text(recent?"↗":"Reprendre  →",16,Accent,true);arrow.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(arrow,Dock.Right);line.Children.Add(arrow);
                if(recent && design==Prism){
                    var thumbnail=Framed(PrismArt.Panel(46,false,true));
                    thumbnail.Width=74;thumbnail.Margin=new Thickness(0,0,12,0);line.Children.Add(thumbnail);
                }
                AutomationProperties.SetName(button,"Reprendre "+p.DisplayTitle+" · "+Stage(p));
                var details=new StackPanel{VerticalAlignment=VerticalAlignment.Center};details.Children.Add(Text(p.DisplayTitle,16,Fg,true));details.Children.Add(Text(Stage(p),13,Muted));
                line.Children.Add(details);button.Content=line;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,recent?6:0,4);list.Children.Add(button);
            }
        }
        private void Create(){draft=Service.Create();Go("Atelier");}
        private void Styles(){Heading("Ambiances du studio");StyleCards();var toggle=Btn(still?"Activer les animations douces":"Réduire les animations",()=>{still=!still;SaveStyle();Draw();});toggle.HorizontalAlignment=HorizontalAlignment.Left;toggle.Margin=Gap(16);body.Children.Add(toggle);}
        private void StyleCards()
        {
            var row=new UniformGrid{Columns=3};
            foreach(string option in new[]{Prism,"Orbital","Relief"}){
                bool selected=option==design;var box=new StackPanel();box.Children.Add(Framed(Illustration(option,120,true)));
                var line=new DockPanel{Margin=new Thickness(6,7,6,6)};
                var state=Text(selected?"●  Active":"Choisir  →",12,selected?Accent:Muted,true);DockPanel.SetDock(state,Dock.Right);line.Children.Add(state);
                line.Children.Add(Text(option,16,Fg,true));box.Children.Add(line);
                var b=Btn("",()=>{design=option;SaveStyle();Draw();});b.Padding=new Thickness(3);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                if(selected)b.BorderBrush=B(Accent);b.Margin=new Thickness(3,0,3,0);b.Content=box;
                AutomationProperties.SetName(b,"Choisir "+option);row.Children.Add(b);
            }
            body.Children.Add(row);
        }
    }
}
