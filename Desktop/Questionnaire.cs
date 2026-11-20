using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using GameForge.Editor;

namespace GameForge.Desktop
{
    internal sealed partial class StudioWindow
    {
        private TextBox focusAnswer;
        private static string FocusTitle(ForgePromptId id) => id switch {
            ForgePromptId.Concept => "Que fait-on dans ton jeu ?",
            ForgePromptId.Player => "Qui est le personnage ?",
            ForgePromptId.Audience => "Pour qui et pour quelles sessions ?",
            ForgePromptId.World => "Où se déroule ton jeu ?",
            ForgePromptId.Art => "Quel style veux-tu lui donner ?",
            ForgePromptId.Atmosphere => "Quelle ambiance imagines-tu ?",
            ForgePromptId.Readability => "Comment reconnaître les dangers ?",
            ForgePromptId.Enemy => "À quoi ressemblent les ennemis ?",
            ForgePromptId.EnemyBehavior => "Comment réagissent-ils ?",
            ForgePromptId.Encounters => "Comment se présentent les défis ?",
            ForgePromptId.AssetSource => "De quelles ressources as-tu besoin ?",
            ForgePromptId.Controls => "Comment veut-on jouer ?",
            ForgePromptId.Goal => "Qu’est-ce qui fait gagner ?",
            ForgePromptId.Failure => "Que se passe-t-il quand on perd ?",
            ForgePromptId.Progression => "Comment évolue la difficulté ?",
            ForgePromptId.Audio => "Qu’entend-on pendant la partie ?",
            ForgePromptId.Accessibility => "Comment rendre le jeu confortable ?",
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        private void Question(int chapter,int question)
        {
            if(chapter<0 || chapter>=ForgeBrief.Chapters.Length || question<0 || question>=ForgeBrief.Chapters[chapter].Prompts.Count)
                throw new ArgumentOutOfRangeException(nameof(question));
            draft.briefChapter=chapter;draft.briefQuestion=question;Service.Save(draft);Draw();
        }
        private void MoveQuestion(int direction)
        {
            int c=draft.briefChapter,q=draft.briefQuestion+direction;
            if(q<0){if(c==0){Back(ForgeStage.Idea);return;} c--;q=ForgeBrief.Chapters[c].Prompts.Count-1;}
            if(q>=ForgeBrief.Chapters[c].Prompts.Count){if(c==ForgeBrief.Chapters.Length-1){FinishQuestions();return;}c++;q=0;}
            Question(c,q);
        }
        private void FinishQuestions()
        {
            int missing=draft.brief.MissingChapter;
            if(missing>=0){
                var prompts=ForgeBrief.Chapters[missing].Prompts;
                int index=Enumerable.Range(0,prompts.Count).First(i=>prompts[i].Required && string.IsNullOrWhiteSpace(draft.brief.Get(prompts[i].Id)));
                Question(missing,index);Status("Complète cette réponse requise ou laisse GameForge proposer.");return;
            }
            Service.ProposeLooks(draft);Draw();
        }
        private void Questions()
        {
            var chapters=ForgeBrief.Chapters;int c=draft.briefChapter,q=draft.briefQuestion;
            var prompt=chapters[c].Prompts[q];
            var layout=new Grid{Margin=Gap(4,16)};
            layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(166)});
            layout.ColumnDefinitions.Add(new ColumnDefinition());
            layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(234)});
            for(int i=0;i<3;i++)layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var chaptersNav=new WrapPanel{Orientation=Orientation.Vertical,Margin=new Thickness(0,0,16,0)};
            var chapterCounts=new TextBlock[chapters.Length];
            var chapterButtons=new Button[chapters.Length];
            AutomationProperties.SetName(chaptersNav,"Chapitres du questionnaire");
            for(int i=0;i<chapters.Length;i++){
                int position=i;int answered=chapters[i].Prompts.Count(p=>!string.IsNullOrWhiteSpace(draft.brief.Get(p.Id)));
                var text=new StackPanel();text.Children.Add(Text((i+1)+"  "+chapters[i].Title,13,i==c?(light?"#FFFFFF":"#201A28"):Fg,true));
                chapterCounts[i]=Text(answered+" / "+chapters[i].Prompts.Count+" réponses",11,i==c?(light?"#E5E1F4":"#35493E"):Muted);
                text.Children.Add(chapterCounts[i]);
                var button=Btn("",()=>Chapter(position),i==c,true);button.Content=text;
                button.Padding=new Thickness(10,10,10,10);button.Margin=new Thickness(0,0,4,8);
                AutomationProperties.SetName(button,chapters[i].Title+", "+answered+" réponses");chaptersNav.Children.Add(button);
                chapterButtons[i]=button;
            }
            var fields=new StackPanel();
            fields.Children.Add(Text("QUESTION "+(q+1)+" SUR "+chapters[c].Prompts.Count+"  ·  "+chapters[c].Title.ToUpperInvariant(),12,Accent,true));
            var count=Text("",12,Muted);var summary=new StackPanel();
            void UpdateSummary(){
                int total=chapters.Sum(ch=>ch.Prompts.Count),answered=chapters.Sum(ch=>ch.Prompts.Count(p=>!string.IsNullOrWhiteSpace(draft.brief.Get(p.Id))));
                count.Text=answered+" / "+total+" réponses · brouillon enregistré";
                for(int i=0;i<chapters.Length;i++){
                    int filled=chapters[i].Prompts.Count(p=>!string.IsNullOrWhiteSpace(draft.brief.Get(p.Id)));
                    chapterCounts[i].Text=filled+" / "+chapters[i].Prompts.Count+" réponses";
                    AutomationProperties.SetName(chapterButtons[i],chapters[i].Title+", "+filled+" réponses");
                }
                summary.Children.Clear();summary.Children.Add(Text("TON JEU, JUSQU’ICI",11,Accent,true));
                var title=Text("Les premières briques",19,Fg,true);title.Margin=Gap(8,16);summary.Children.Add(title);
                AddSummary("Ton idée",draft.idea);
                foreach(var item in new[]{(ForgePromptId.Player,"Personnage"),(ForgePromptId.World,"Univers"),(ForgePromptId.Enemy,"Adversaires"),(ForgePromptId.Goal,"Réussite")})
                    AddSummary(item.Item2,draft.brief.Get(item.Item1));
                var hint=Text("Après le brief, tu compareras quatre apparences, puis quatre façons de jouer.",13,Muted);hint.Margin=Gap(14);summary.Children.Add(hint);
            }
            void AddSummary(string label,string value){
                summary.Children.Add(Text(label,12,Accent,true));
                string text=string.IsNullOrWhiteSpace(value)?"À imaginer ensemble":value==ForgeBrief.DelegateAnswer?"GameForge proposera ce point":value;
                var line=Text(text.Length>130?text.Substring(0,127)+"…":text,13,Muted);line.Margin=Gap(3,13);line.ToolTip=text;summary.Children.Add(line);
            }
            focusAnswer=Input(fields,FocusTitle(prompt.Id),draft.brief.Get(prompt.Id),ForgeBrief.AnswerLimit,true,v=>{
                draft.brief.Set(prompt.Id,v);Service.Save(draft);UpdateSummary();
            },26);
            AutomationProperties.SetName(focusAnswer,prompt.Question);
            var help=Text(prompt.Question,14,Muted);help.Margin=Gap(8);
            fields.Children.Insert(fields.Children.IndexOf(focusAnswer),help);
            focusAnswer.MinHeight=164;focusAnswer.MaxHeight=260;focusAnswer.FontSize=16;focusAnswer.Margin=Gap(14,8);
            if(!Resources.Contains("FocusAnswerSkin"))Resources["FocusAnswerSkin"]=XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'><Border x:Name='outline' CornerRadius='12' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='outline' Property='BorderBrush' Value='#73AA98'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            focusAnswer.Template=(ControlTemplate)Resources["FocusAnswerSkin"];
            var example=Text("Exemple · "+prompt.Example,13,Muted);example.Margin=Gap(0,12);fields.Children.Add(example);
            fields.Children.Add(Text(prompt.Required?"Réponse requise · tu peux la confier à GameForge.":"Facultatif · tu peux y revenir plus tard.",12,Muted));
            var tools=new WrapPanel{Margin=Gap(10,8)};VoiceButton(tools,focusAnswer);
            var answer=focusAnswer;tools.Children.Add(Btn("Laisser GameForge proposer",()=>answer.Text=ForgeBrief.DelegateAnswer,false,true));
            fields.Children.Add(tools);
            var keep=new CheckBox{Content="Garder ce choix",Foreground=B(Fg),IsChecked=draft.brief.IsLocked(prompt.Id),Margin=Gap(0,14)};
            keep.Click+=(s,e)=>Safe(()=>{draft.brief.SetLocked(prompt.Id,keep.IsChecked==true);Service.Save(draft);});fields.Children.Add(keep);
            var actions=new DockPanel{LastChildFill=false,Margin=Gap(8,14)};
            var previous=Btn(c==0 && q==0?"← Mon idée":"← Précédente",()=>MoveQuestion(-1));actions.Children.Add(previous);
            bool last=c==chapters.Length-1 && q==chapters[c].Prompts.Count-1;
            var next=Btn(last?"Voir les quatre apparences →":"Continuer →",()=>MoveQuestion(1),true);
            DockPanel.SetDock(next,Dock.Right);actions.Children.Add(next);fields.Children.Add(actions);fields.Children.Add(count);
            if(last){var ai=AiButton(true,false);ai.Margin=Gap(12);fields.Children.Add(ai);}
            var card=Card(fields,24);card.VerticalAlignment=VerticalAlignment.Top;card.Margin=new Thickness(0,0,20,0);
            layout.Children.Add(chaptersNav);layout.Children.Add(card);layout.Children.Add(summary);body.Children.Add(layout);UpdateSummary();
            bool? previousCompact=null;
            void ArrangeFocus(double width){
                bool compact=width<1040;if(previousCompact==compact)return;previousCompact=compact;
                layout.ColumnDefinitions[0].Width=new GridLength(compact?0:166);
                layout.ColumnDefinitions[2].Width=new GridLength(compact?0:234);
                chaptersNav.Orientation=compact?Orientation.Horizontal:Orientation.Vertical;
                Grid.SetColumn(chaptersNav,0);Grid.SetColumnSpan(chaptersNav,compact?3:1);
                Grid.SetColumn(card,compact?0:1);Grid.SetRow(card,compact?1:0);Grid.SetColumnSpan(card,compact?3:1);
                Grid.SetColumn(summary,compact?0:2);Grid.SetRow(summary,compact?2:0);Grid.SetColumnSpan(summary,compact?3:1);
                card.Margin=new Thickness(0,0,compact?0:20,compact?22:0);
            }
            ArrangeFocus(Math.Max(0,ActualWidth-60));layout.SizeChanged+=(s,e)=>ArrangeFocus(e.NewSize.Width);
        }
    }
}
