using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameForge.Editor;

namespace GameForge.Desktop
{
    internal sealed partial class StudioWindow
    {
        internal void Photograph(string folder,string style,bool bright)
        {
            design=style;light=bright;page="Accueil";Draw();SizeToContent=SizeToContent.Manual;
            var size=new Size(1200,764);shell.Measure(size);shell.Arrange(new Rect(size));shell.UpdateLayout();
            var bmp=new RenderTargetBitmap(1200,764,96,96,PixelFormats.Pbgra32);
            bmp.Render(shell);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
            using var target=File.Create(Path.Combine(folder,style+(bright?"-clair":"-sombre")+".png"));png.Save(target);
        }
        private static Button CreationButton(DependencyObject parent)
        {
            if(parent is Button button && AutomationProperties.GetName(button)=="Créer un jeu")return button;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){
                var match=CreationButton(VisualTreeHelper.GetChild(parent,i));
                if(match!=null)return match;
            }
            return null;
        }
        internal static class Verification
        {
            [STAThread]
            public static int Main(string[] args)
            {
                try{
                    var app=new Application();var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
                    var rt=new StudioRuntime(root);var win=new StudioWindow(rt,true);
                    if(args.Length>1 && (args[1]=="android" || args[1]=="windows")){
                        var complete=rt.Projects.List().First(p=>p.stage==ForgeStage.Complete);
                        System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                        Console.WriteLine(rt.BuildGame(complete,args[1]=="android").GetAwaiter().GetResult());return 0;
                    }
                    if(args.Length>1 && args[1]=="generate"){
                        var draft=rt.Projects.List().First(p=>p.stage==ForgeStage.Gameplay);
                        var editor=rt.FindEditor()??throw new Exception("Éditeur compatible introuvable");
                        System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                        var done=rt.Generate(editor,draft,2).GetAwaiter().GetResult();
                        Console.WriteLine("Scène créée et relue : "+done.scenePath);return 0;
                    }
                    if(win.design!=Prism || win.nav.Orientation!=Orientation.Horizontal)throw new Exception("Accueil Prisme attendu");
                    win.Photograph(root,Prism,false);
                    var create=CreationButton(win.shell)??throw new Exception("Bouton de création absent");
                    create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var p=win.draft??throw new Exception("Le bouton doit créer un brouillon");
                    p.title="Les jardins de Minuit";p.idea="Guider un renard entre les ruines.";rt.Projects.Save(p);
                    rt.Projects.StartQuestions(p);
                    foreach(var prompt in ForgeBrief.Chapters.SelectMany(c=>c.Prompts).Where(q=>q.Required)) p.brief.Set(prompt.Id,prompt.Example);
                    p.briefChapter=2;p.brief.SetLocked(ForgePromptId.Enemy,true);rt.Projects.Save(p);win.Draw();
                    win.Chapter(3);if(rt.Projects.Read(p.id).briefChapter!=3)throw new Exception("Reprise du chapitre");
                    rt.Projects.ProposeLooks(p);rt.Projects.ProposeLooks(p,true);
                    if(p.looks.Length!=4||p.lookRound!=1)throw new Exception("Série d’apparences");
                    rt.Projects.ChooseLook(p,1);rt.Projects.RegenerateGameplay(p);win.Draw();
                    if(p.playStyles.Length!=4||!p.playStyles.All(q=>q.config.skyColor==p.looks[1].config.skyColor))throw new Exception("Apparence modifiée");
                    if(Directory.Exists(rt.EngineProject))throw new Exception("Moteur préparé avant la génération");
                    var q2=rt.Projects.Create();q2.title="Traversée d’ambre";rt.Projects.Save(q2);
                    foreach(string style in new[]{Prism,"Orbital","Relief"}) foreach(bool bright in new[]{false,true})win.Photograph(root,style,bright);
                    Console.WriteLine("Accueil Prisme, création, sauvegarde, questionnaire, reprise, 4+4 propositions et six rendus vérifiés.");return 0;
                }catch(Exception e){Console.Error.WriteLine(e);return 1;}
            }
        }
    }
}
