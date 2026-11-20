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
        internal void Photograph(string folder,string style,bool bright,bool questionnaire=false,int width=1200)
        {
            design=style;light=bright;page=questionnaire?"Atelier":"Accueil";Draw();SizeToContent=SizeToContent.Manual;
            var size=new Size(width,764);shell.Measure(size);shell.Arrange(new Rect(size));shell.UpdateLayout();
            if(questionnaire && width<1000){
                var next=NamedButton(shell,"Continuer →")??throw new Exception("Navigation Focus absente");
                next.BringIntoView();shell.UpdateLayout();
                var bounds=next.TransformToAncestor(contentScroll).TransformBounds(new Rect(next.RenderSize));
                if(bounds.Top<0 || bounds.Bottom>contentScroll.ViewportHeight+1)throw new Exception("Navigation Focus inaccessible après défilement");
                contentScroll.ScrollToTop();shell.UpdateLayout();
            }
            var bmp=new RenderTargetBitmap(width,764,96,96,PixelFormats.Pbgra32);
            bmp.Render(shell);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
            using var target=File.Create(Path.Combine(folder,(questionnaire?"Focus":style)+(bright?"-clair":"-sombre")+(width<1000?"-compact":"")+".png"));png.Save(target);
        }
        private static Button NamedButton(DependencyObject parent,string name)
        {
            if(parent is Button button && (AutomationProperties.GetName(button)==name || Equals(button.Content,name)))return button;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){
                var match=NamedButton(VisualTreeHelper.GetChild(parent,i),name);
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
                    var create=NamedButton(win.shell,"Créer un jeu")??throw new Exception("Bouton de création absent");
                    create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var p=win.draft??throw new Exception("Le bouton doit créer un brouillon");
                    p.title="Les jardins de Minuit";p.idea="Guider un renard entre les ruines.";rt.Projects.Save(p);
                    rt.Projects.StartQuestions(p);
                    win.page="Atelier";win.Draw();
                    win.shell.Measure(new Size(1000,540));win.shell.Arrange(new Rect(0,0,1000,540));win.shell.UpdateLayout();
                    if(win.contentScroll.VerticalScrollBarVisibility!=ScrollBarVisibility.Hidden || win.contentScroll.ScrollableHeight<=0)
                        throw new Exception("Le questionnaire doit rester défilable sans barre visible");
                    win.contentScroll.ScrollToEnd();win.shell.UpdateLayout();
                    if(win.contentScroll.VerticalOffset<=0)throw new Exception("Le défilement est bloqué");
                    win.still=false;win.MoveAura(new Point(210,180));
                    bool canAnimate=SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
                    if(canAnimate && win.pointerAura.Visibility!=Visibility.Visible)
                        throw new Exception("Aura absente au survol");
                    if(win.pointerAura.IsHitTestVisible || (canAnimate && ((TranslateTransform)win.pointerAura.RenderTransform).X!=120))
                        throw new Exception("Aura mal placée ou interception des clics");
                    if(!canAnimate && win.pointerAura.Visibility!=Visibility.Collapsed)throw new Exception("Confort système ignoré");
                    win.still=true;win.MoveAura(new Point(210,180));
                    if(win.pointerAura.Visibility!=Visibility.Collapsed)throw new Exception("Réduction des mouvements ignorée");
                    var voiceForm=new StackPanel();
                    var spoken=win.Input(voiceForm,"Réponse de test","Bonjour, mon jeu",100,true,v=>{});
                    win.VoiceButton(voiceForm,spoken);win.body.Children.Add(voiceForm);spoken.Select(9,3);
                    voiceForm.Children.OfType<Button>().Single(b=>Equals(b.Content,"Dicter avec Windows"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if(spoken.Text!="Bonjour, mon jeu" || spoken.SelectionStart!=9 || spoken.SelectionLength!=3 ||
                        !win.notice.Text.Contains("Windows + H"))throw new Exception("La préparation vocale doit conserver la réponse sans ouvrir de micro");
                    if(System.Runtime.InteropServices.Marshal.SizeOf<WindowsDictation.Input>()!=(IntPtr.Size==8?40:28))
                        throw new Exception("Structure native INPUT incorrecte");
                    var shortcut=WindowsDictation.Shortcut();
                    if(shortcut.Length!=4 || shortcut.Any(k=>k.Type!=1) ||
                        !shortcut.Select(k=>k.Data.Keyboard.Key).SequenceEqual(new ushort[]{0x5B,0x48,0x48,0x5B}) ||
                        !shortcut.Select(k=>k.Data.Keyboard.Flags).SequenceEqual(new uint[]{0,0,2,2}))
                        throw new Exception("Le raccourci vocal doit libérer les touches H et Windows");
                    bool refused=false;
                    try{WindowsDictation.Open(win,spoken);}catch(InvalidOperationException){refused=true;}
                    if(!refused)throw new Exception("La dictée ne doit pas cibler une fenêtre de test sans focus");
                    win.focusAnswer.Text="Guider un renard entre les ruines.";win.MoveQuestion(1);
                    if(p.briefQuestion!=1 || rt.Projects.Read(p.id).briefQuestion!=1)throw new Exception("Question suivante non sauvegardée");
                    win.focusAnswer.Text="Un renard turquoise avec un sac carré.";
                    win.draft=rt.Projects.Read(p.id);win.Draw();p=win.draft;
                    if(win.focusAnswer.Text!="Un renard turquoise avec un sac carré." ||
                        AutomationProperties.GetName(win.focusAnswer)!=ForgeBrief.Chapters[0].Prompts[1].Question)
                        throw new Exception("La reprise doit retrouver la question exacte et sa réponse");
                    win.MoveQuestion(-1);
                    if(win.focusAnswer.Text!="Guider un renard entre les ruines.")throw new Exception("Réponse précédente perdue");
                    win.Question(2,3);win.MoveQuestion(1);
                    if(p.briefChapter!=3 || p.briefQuestion!=0)throw new Exception("Passage entre chapitres incorrect");
                    win.MoveQuestion(-1);
                    if(p.briefChapter!=2 || p.briefQuestion!=3)throw new Exception("Retour entre chapitres incorrect");
                    win.Question(4,1);win.FinishQuestions();
                    if(p.briefChapter!=1 || p.briefQuestion!=0 || p.stage!=ForgeStage.Questions)
                        throw new Exception("Le brief incomplet doit ouvrir la première question requise manquante");
                    foreach(int invalid in new[]{-1,ForgeBrief.Chapters[1].Prompts.Count}){
                        p.briefQuestion=invalid;bool rejected=false;
                        try{rt.Projects.Save(p);}catch(InvalidDataException){rejected=true;}
                        if(!rejected)throw new Exception("Curseur de question invalide accepté");
                    }
                    p=rt.Projects.Read(p.id);win.draft=p;
                    var legacy=rt.Projects.Create();var legacyPath=Path.Combine(root,".local","projects",legacy.id+".json");
                    var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(legacyPath));json.Remove("briefQuestion");
                    File.WriteAllText(legacyPath,json.ToString());
                    if(rt.Projects.Read(legacy.id).briefQuestion!=0)throw new Exception("Ancien brouillon incompatible");
                    for(int chapter=0;chapter<ForgeBrief.Chapters.Length;chapter++)
                        for(int question=0;question<ForgeBrief.Chapters[chapter].Prompts.Count;question++){
                            win.Question(chapter,question);
                            if(AutomationProperties.GetName(win.focusAnswer)!=ForgeBrief.Chapters[chapter].Prompts[question].Question)
                                throw new Exception("Question Focus manquante ou incorrecte");
                        }
                    foreach(var prompt in ForgeBrief.Chapters.SelectMany(c=>c.Prompts).Where(q=>q.Required)) p.brief.Set(prompt.Id,prompt.Example);
                    p.briefChapter=2;p.briefQuestion=0;p.brief.SetLocked(ForgePromptId.Enemy,true);rt.Projects.Save(p);win.Draw();
                    foreach(bool bright in new[]{false,true})win.Photograph(root,Prism,bright,true);
                    win.Photograph(root,Prism,false,true,720);
                    win.Question(2,3);win.Chapter(4);
                    if(rt.Projects.Read(p.id).briefChapter!=4 || rt.Projects.Read(p.id).briefQuestion!=0 || !p.brief.IsLocked(ForgePromptId.Enemy))
                        throw new Exception("Changement de chapitre ou conservation des verrous incorrect");
                    rt.Projects.ProposeLooks(p);rt.Projects.ProposeLooks(p,true);
                    if(p.looks.Length!=4||p.lookRound!=1)throw new Exception("Série d’apparences");
                    rt.Projects.ChooseLook(p,1);rt.Projects.RegenerateGameplay(p);win.Draw();
                    if(p.playStyles.Length!=4||!p.playStyles.All(q=>q.config.skyColor==p.looks[1].config.skyColor))throw new Exception("Apparence modifiée");
                    if(Directory.Exists(rt.EngineProject))throw new Exception("Moteur préparé avant la génération");
                    var q2=rt.Projects.Create();q2.title="Traversée d’ambre";rt.Projects.Save(q2);
                    foreach(string style in new[]{Prism,"Orbital","Relief"}) foreach(bool bright in new[]{false,true})win.Photograph(root,style,bright);
                    Console.WriteLine("Focus : 17 questions, navigation, reprise exacte, anciens brouillons, verrous et réponses requises vérifiés. Accueil, aura, défilement, 4+4 propositions et neuf rendus vérifiés. Micro non activé.");return 0;
                }catch(Exception e){Console.Error.WriteLine(e);return 1;}
            }
        }
    }
}
