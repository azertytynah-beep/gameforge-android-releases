using System;
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
using System.Windows.Media;
using GameForge.Editor;
using Microsoft.Win32;

namespace GameForge.Desktop
{
    internal sealed partial class StudioWindow
    {
        private TextBox Input(StackPanel panel,string label,string value,int limit,bool multi,Action<string> changed)
        {
            var name=Text(label,16,Fg,true);name.Margin=Gap(8,5);panel.Children.Add(name);
            var field=new TextBox{Text=value??"",MaxLength=limit,AcceptsReturn=multi,TextWrapping=TextWrapping.Wrap,
                MinHeight=multi?62:36,Padding=new Thickness(9,7,9,7),FontSize=15,Background=B(Bg),Foreground=B(Fg),
                CaretBrush=B(Fg),BorderBrush=B(Edge),BorderThickness=new Thickness(1),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            AutomationProperties.SetName(field,label);field.TextChanged+=(s,e)=>Safe(()=>changed(field.Text));panel.Children.Add(field);return field;
        }
        private void Actions(params Button[] buttons)
        {
            var row=new WrapPanel{Margin=Gap(12,5)};
            foreach(var b in buttons){b.Margin=new Thickness(0,0,8,6);row.Children.Add(b);}body.Children.Add(row);
        }
        private void Workshop()
        {
            Heading(draft.DisplayTitle);
            var step=Text(Stage(draft)+"   ·   "+((int)draft.stage+1)+" / 5",14,Accent,true);
            step.Margin=Gap(0,12);body.Children.Add(step);
            switch(draft.stage){
                case ForgeStage.Idea: Idea(); break;
                case ForgeStage.Questions: Questions(); break;
                case ForgeStage.Appearance: Choices(true);break;
                case ForgeStage.Gameplay: Choices(false);break;
                case ForgeStage.Complete:
                    body.Children.Add(Text("Ta scène jouable est prête.",23,Fg,true));
                    body.Children.Add(Text(draft.scenePath,14,Muted));
                    Actions(Btn("Jouer sur PC",async ()=>await BuildGame(false),true),
                        Btn("Exporter l’APK de test",async ()=>await BuildGame(true)),
                        Btn("Ouvrir le dossier du jeu",()=>OpenFolder(Path.GetDirectoryName(Path.Combine(studio.EngineProject,draft.scenePath)))),
                        Btn("Mes projets",()=>Go("Mes projets")));break;
            }
        }
        private static void OpenFolder(string path){Process.Start("explorer.exe","\""+path+"\"");}
        private void Idea()
        {
            var fields=new StackPanel();
            Input(fields,"Nom du jeu (facultatif)",draft.title,60,false,v=>{draft.title=v; Service.Save(draft);});
            var idea=Input(fields,"Quel jeu aimerais-tu créer ?",draft.idea,ForgeProjectStore.MaxIdeaLength,true,v=>{draft.idea=v;Service.Save(draft);});
            VoiceButton(fields,idea);body.Children.Add(Card(fields));
            Actions(Btn("Continuer vers le questionnaire  →",()=>{Service.StartQuestions(draft);Draw();},true),
                Btn("Trouver une idée",async ()=>await Ideas(idea)),Btn("Partir du modèle Runner",()=>{draft.start=ForgeStart.Choose;Service.StartQuestions(draft);Draw();}));
        }
        private async Task Ideas(TextBox field)
        {
            await Request(async token=>{
                var ideas=await Service.SuggestIdeasAsync(field.Text,token);
                foreach(var option in ideas.Ideas){var b=Btn(option,()=>{field.Text=option;});b.Margin=Gap(3);body.Children.Add(b);}
            },"Recherche d’idées…");
        }
        private void Questions()
        {
            var chapters=ForgeBrief.Chapters;var c=draft.briefChapter;
            var tabs=new WrapPanel{Margin=Gap(0,10)};
            for(int i=0;i<chapters.Length;i++){
                int pos=i;var b=Btn(chapters[i].Title,()=>Chapter(pos),i==c);b.Padding=new Thickness(10,6,10,6);b.Margin=new Thickness(0,0,4,4);tabs.Children.Add(b);
            }
            body.Children.Add(tabs);
            foreach(var prompt in chapters[c].Prompts){
                var fields=new StackPanel();
                var field=Input(fields,prompt.Question+(prompt.Required?" *":""),draft.brief.Get(prompt.Id),ForgeBrief.AnswerLimit,true,v=>{draft.brief.Set(prompt.Id,v);Service.Save(draft);});
                var example=Text("Ex. "+prompt.Example,13,Muted);example.Margin=Gap(4);fields.Children.Add(example);
                var actions=new WrapPanel{Margin=Gap(4)};VoiceButton(actions,field);
                var delegateBtn=Btn("Laisser GameForge proposer",()=>field.Text=ForgeBrief.DelegateAnswer,false,true);
                actions.Children.Add(delegateBtn);
                var keep=new CheckBox{Content="Verrouiller ce choix",VerticalAlignment=VerticalAlignment.Center,Foreground=B(Fg),
                    IsChecked=draft.brief.IsLocked(prompt.Id),Margin=new Thickness(9,0,0,0)};
                keep.Click+=(s,e)=>Safe(()=>{draft.brief.SetLocked(prompt.Id,keep.IsChecked==true);Service.Save(draft);});actions.Children.Add(keep);
                fields.Children.Add(actions);var card=Card(fields,13);card.Margin=Gap(0,7);body.Children.Add(card);
            }
            var back=Btn(c==0?"Revoir mon idée":"←  Précédent",()=>{if(c==0)Back(ForgeStage.Idea);else Chapter(c-1);});
            if(c<chapters.Length-1)Actions(back,Btn("Chapitre suivant  →",()=>Chapter(c+1),true));
            else Actions(back,Btn("Voir les quatre apparences",()=>{Service.ProposeLooks(draft);Draw();},true),AiButton(true,false));
        }
        private void Chapter(int i){draft.briefChapter=i;Service.Save(draft);Draw();}
        private void Back(ForgeStage to)
        {
            if(!string.IsNullOrEmpty(draft.variantId))throw new InvalidOperationException("Reprends la génération en cours.");
            if(to<ForgeStage.Gameplay){draft.playStyles=null;draft.chosenPlay=-1;}
            if(to<ForgeStage.Appearance){draft.looks=null;draft.chosenLook=-1;}
            draft.stage=to;Service.Save(draft);Draw();
        }
        private void Choices(bool looks)
        {
            var options=looks?draft.looks:draft.playStyles;
            body.Children.Add(Text(looks?"Quelle apparence pour ton jeu ?":"Comment veux-tu jouer ?",23,Fg,true));
            if(!looks)body.Children.Add(Text("Apparence retenue · "+draft.looks[draft.chosenLook].label,14,Muted));
            var grid=new UniformGrid{Columns=2,Margin=Gap(10)};
            for(int i=0;i<options.Length;i++){
                int index=i;var opt=options[i];var column=new StackPanel();
                if(looks){
                    var art=new Grid{Height=78,Background=B(opt.config.skyColor),ClipToBounds=true};
                    var scene=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 0,78 L 41,25 L 63,25 L 100,78 Z"),Fill=B(opt.config.trackColor),Stretch=Stretch.Fill,
                        Margin=new Thickness(10,24,10,-4)};art.Children.Add(scene);
                    var obstacle=new Border{Width=16,Height=15,Background=B(opt.config.obstacleColor),HorizontalAlignment=HorizontalAlignment.Center,
                        VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(33,22,0,0)};art.Children.Add(obstacle);column.Children.Add(art);
                }
                var header=Text(opt.label,18,Fg,true);header.Margin=Gap(6);column.Children.Add(header);
                column.Children.Add(Text(opt.summary,14));column.Children.Add(Text(opt.detail,13,Muted));
                var choose=Btn(looks?"Retenir cette apparence":"Créer ce jeu avec Unity",async ()=>{
                    if(looks){Safe(()=>{Service.ChooseLook(draft,index);Draw();});}else await Generate(index);
                },true);
                choose.IsEnabled=string.IsNullOrEmpty(draft.variantId)||index==draft.chosenPlay;
                choose.Margin=Gap(9);column.Children.Add(choose);var card=Card(column,13);card.Margin=new Thickness(3);grid.Children.Add(card);
            }
            body.Children.Add(grid);
            if(string.IsNullOrEmpty(draft.variantId)) Actions(Btn(looks?"Revoir le questionnaire":"Revoir les apparences",()=>Back(looks?ForgeStage.Questions:ForgeStage.Appearance)),
                Btn("Quatre autres propositions",()=>{if(looks)Service.ProposeLooks(draft,true);else Service.RegenerateGameplay(draft);Draw();}),AiButton(looks,true));
        }
        private Button AiButton(bool looks,bool regenerate)
        {
            return Btn("Proposer avec OmniRoute",async ()=>{
                if(!Service.HasOmniConfiguration){Status("Renseigne la passerelle et le modèle OmniRoute dans Réglages, puis reprends ton projet.");return;}
                await Request(async token=>{
                    if(looks)await Service.ProposeLooksWithAiAsync(draft,regenerate,token);
                    else await Service.ProposeGameplayWithAiAsync(draft,regenerate,token);
                    Draw();
                },"Envoi du brief au modèle OmniRoute sélectionné…");
            });
        }
        private async Task Request(Func<CancellationToken,Task> run,string message)
        {
            if(busy)return;
            StopVoice();busy=true;request=new CancellationTokenSource();
            var cancel=Btn("Annuler",()=>request?.Cancel());var holder=(DockPanel)notice.Parent;
            DockPanel.SetDock(cancel,Dock.Bottom);holder.Children.Insert(0,cancel);
            body.IsEnabled=false;Status(message);
            try{await run(request.Token);Status("");}
            catch(OperationCanceledException){Status("Demande annulée.");}
            catch(Exception e){Status(e.GetBaseException().Message);}
            finally{holder.Children.Remove(cancel);body.IsEnabled=true;busy=false;request.Dispose();request=null;}
        }
        private async Task Generate(int i)
        {
            if(busy)return;
            var editor=studio.FindEditor();
            if(editor==null){Status("Unity "+StudioRuntime.UnityVersion+" est nécessaire pour créer la scène. Indique son emplacement dans Réglages.");return;}
            StopVoice();busy=true;shell.IsEnabled=false;Status("Unity crée la scène en arrière-plan. Le premier import du moteur peut prendre plusieurs minutes.");
            try{draft=await studio.Generate(editor,draft,i);Draw();}
            catch(Exception e){draft=Service.Read(draft.id);Draw();Status(e.GetBaseException().Message+"  "+studio.Log);}
            finally{busy=false;shell.IsEnabled=true;}
        }
        private async Task BuildGame(bool android)
        {
            if(busy)return;
            busy=true;shell.IsEnabled=false;Status(android?"Unity prépare l’APK de test en arrière-plan…":"Unity prépare le Runner pour Windows…");
            try{
                var file=await studio.BuildGame(draft,android);
                if(android)OpenFolder(Path.GetDirectoryName(file));
                else Process.Start(new ProcessStartInfo(file,"-screen-fullscreen 0 -screen-width 450 -screen-height 800"){WorkingDirectory=Path.GetDirectoryName(file)});
                Status(android?"APK de test prêt.":"Aperçu Windows prêt.");
            }catch(Exception e){Status(e.GetBaseException().Message+"  "+studio.Log);}
            finally{busy=false;shell.IsEnabled=true;}
        }
        private void VoiceButton(Panel panel,TextBox field)
        {
            var b=Btn("Dicter en français",()=>{},false,true);
            b.Click+=(s,e)=>Safe(()=>{
                bool same=ReferenceEquals(b,voiceButton);StopVoice();if(same)return;
                var fr=SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r=>r.Culture.Name=="fr-FR");
                if(fr==null)throw new InvalidOperationException("La dictée française est indisponible sur ce PC.");
                var engine=new SpeechRecognitionEngine(fr);
                try{engine.LoadGrammar(new DictationGrammar());engine.SetInputToDefaultAudioDevice();
                    engine.SpeechRecognized+=(sender,eventArgs)=>Dispatcher.BeginInvoke(new Action(()=>{
                        if(!ReferenceEquals(mic,engine))return;
                        string value=(field.Text+" "+eventArgs.Result.Text).Trim();
                        field.Text=value.Substring(0,Math.Min(field.MaxLength,value.Length));field.CaretIndex=field.Text.Length;
                    }));
                    engine.RecognizeCompleted+=(sender,args)=>Dispatcher.BeginInvoke(new Action(()=>{if(ReferenceEquals(mic,engine))StopVoice();}));
                    engine.RecognizeAsync(RecognizeMode.Multiple);mic=engine;voiceButton=b;b.Content="Arrêter la dictée";micTimer.Start();}
                catch{engine.Dispose();throw;}
            });panel.Children.Add(b);
        }
        private Button voiceButton;
        private void StopVoice()
        {
            micTimer.Stop();var old=mic;mic=null;
            if(voiceButton!=null){voiceButton.Content="Dicter en français";voiceButton=null;}
            if(old!=null){try{old.RecognizeAsyncCancel();}finally{old.Dispose();}}
        }
        private void Settings()
        {
            Heading("Réglages");var form=new StackPanel();
            form.Children.Add(Text("Création avec Unity",21,Fg,true));
            form.Children.Add(Text(studio.FindEditor()??"Le studio fonctionne. Sélectionne Unity pour générer une scène.",14,Muted));
            var pick=Btn("Choisir Unity.exe",()=>{
                var dialog=new OpenFileDialog{Filter="Éditeur Unity|Unity.exe",CheckFileExists=true};
                if(dialog.ShowDialog(this)!=true)return;
                if(!StudioRuntime.IsCompatibleEditor(dialog.FileName))throw new InvalidOperationException("Sélectionne Unity "+StudioRuntime.UnityVersion+".");
                Preferences.SetString("Unity",dialog.FileName);Draw();
            });pick.HorizontalAlignment=HorizontalAlignment.Left;pick.Margin=Gap(6,12);form.Children.Add(pick);
            form.Children.Add(Text("OmniRoute",21,Fg,true));form.Children.Add(Text("Le bouton Proposer avec OmniRoute envoie ton brief au modèle choisi. La clé se lit dans GAMEFORGE_OMNIROUTE_API_KEY.",14,Muted));
            var url=Input(form,"Adresse de la passerelle",Service.OmniUrl,2048,false,v=>{});
            var model=Input(form,"Modèle",Service.OmniModel,256,false,v=>{});
            var save=Btn("Enregistrer",()=>{ForgeOmniRoute.ResolveEndpoint(url.Text);Service.OmniUrl=url.Text;Service.OmniModel=model.Text;Status("Réglages enregistrés.");},true);
            save.HorizontalAlignment=HorizontalAlignment.Left;save.Margin=Gap(8);form.Children.Add(save);body.Children.Add(Card(form));
            Actions(Btn("Ouvrir les fichiers de l’atelier",()=>{Directory.CreateDirectory(studio.Root);OpenFolder(studio.Root);}),
                Btn("Importer un ancien brouillon",Import));
        }
        private void Import()
        {
            var dialog=new OpenFileDialog{Title="Choisir le fichier JSON dans .local/projects de l’ancien atelier",Filter="Brouillon GameForge|*.json"};
            if(dialog.ShowDialog(this)!=true)return;
            var store=new ForgeProjectStore(Path.GetDirectoryName(dialog.FileName));
            var imported=store.Read(Path.GetFileNameWithoutExtension(dialog.FileName));
            var fresh=Service.Create();imported.id=fresh.id;
            if(!string.IsNullOrEmpty(imported.variantId)){imported.variantId=null;imported.scenePath=null;imported.chosenPlay=-1;imported.stage=ForgeStage.Gameplay;}
            Service.Save(imported);draft=imported;Go("Atelier");
        }
    }
}
