using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace GameForge.Desktop
{
    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            using var unique=new Mutex(true,@"Local\GameForgeAI_Studio",out bool owner);
            if(!owner){MessageBox.Show("Le Studio est déjà ouvert. Retrouve GameForge AI dans la barre des tâches.","GameForge AI");return;}
            try{
                var app=new Application();
                app.Run(new StudioWindow(new StudioRuntime(StudioRuntime.DefaultRoot)));
            }catch(Exception e){MessageBox.Show(e.GetBaseException().Message,"GameForge AI",MessageBoxButton.OK,MessageBoxImage.Error);}
        }
    }
}
