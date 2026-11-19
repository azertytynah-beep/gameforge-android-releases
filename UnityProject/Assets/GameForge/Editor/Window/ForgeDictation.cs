using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    internal sealed class ForgeDictation : IDisposable
    {
        private const string Unavailable = "Dictée indisponible. Le clavier reste utilisable.";
        private const string MissingFrench = "Reconnaissance vocale française indisponible sur ce PC. Utilise le clavier.";
        private const string EndOfOutput = "\0";
        private const string TextPrefix = "TEXT ";
        private const string WindowsWorker = @"
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$engine = $null
try {
    Add-Type -AssemblyName System.Speech
    $french = [System.Speech.Recognition.SpeechRecognitionEngine]::InstalledRecognizers() |
        Where-Object { $_.Culture.TwoLetterISOLanguageName -eq 'fr' } |
        Sort-Object { $_.Culture.Name -ne 'fr-FR' } | Select-Object -First 1
    if ($null -eq $french) {
        [Console]::WriteLine('LANGUAGE')
        return
    }

    $inputStream = [IO.StreamReader]::new([Console]::OpenStandardInput())
    $stop = $inputStream.ReadToEndAsync()
    $engine = [System.Speech.Recognition.SpeechRecognitionEngine]::new($french)
    $engine.LoadGrammar([System.Speech.Recognition.DictationGrammar]::new())
    if ($stop.IsCompleted) { return }

    $null = Register-ObjectEvent -InputObject $engine -EventName SpeechRecognized -SourceIdentifier dictation-text
    $null = Register-ObjectEvent -InputObject $engine -EventName RecognizeCompleted -SourceIdentifier dictation-end
    $engine.SetInputToDefaultAudioDevice()
    $engine.RecognizeAsync([System.Speech.Recognition.RecognizeMode]::Multiple)
    [Console]::WriteLine('READY')
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $stopAt = 0
    :listen while ($true) {
        if (($stop.IsCompleted -or $clock.Elapsed.TotalSeconds -ge 120) -and $stopAt -eq 0) {
            $engine.RecognizeAsyncStop()
            $stopAt = $clock.Elapsed.TotalSeconds + 3
        }
        $event = Wait-Event -Timeout 1
        if ($null -ne $event) {
            Remove-Event -EventIdentifier $event.EventIdentifier
            if ($event.SourceIdentifier -eq 'dictation-end') {
                if ($null -ne $event.SourceEventArgs.Error) { [Console]::WriteLine('ERROR') }
                break listen
            }
            $text = $event.SourceEventArgs.Result.Text
            [Console]::WriteLine('TEXT ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)))
        }
        if ($stopAt -gt 0 -and $clock.Elapsed.TotalSeconds -ge $stopAt) { break listen }
    }
}
catch { [Console]::WriteLine('ERROR') }
finally { if ($null -ne $engine) { $engine.Dispose() } }";
        private static readonly string WorkerArguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsWorker));

        private sealed class Capture
        {
            public TextField Field;
            public Button Button;
            public Label Status;
            public Process Process;
            public readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
            public double Deadline;
            public bool Ready;
            public string Failure;
        }

        private Capture active;
        private bool disposed;

        public ForgeDictation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public VisualElement ButtonFor(TextField field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            var controls = new VisualElement { name = field.name + "-dictation" };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            var button = new Button { name = field.name + "-microphone", text = "Dicter en français" };
            button.tooltip = "Transcrit la voix dans ce champ avec la reconnaissance intégrée à Windows.";
            var status = new Label { name = field.name + "-dictation-status", enableRichText = false };
            status.style.whiteSpace = WhiteSpace.Normal;
            controls.Add(button);
            controls.Add(status);

            bool available = Application.platform == RuntimePlatform.WindowsEditor && !Application.isBatchMode;
            button.SetEnabled(available && !disposed);
            status.text = available ? string.Empty : "Saisie au clavier disponible.";
            button.clicked += () => Toggle(field, button, status);
            controls.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (active?.Button == button)
                    Stop();
            });
            return controls;
        }

        private void Toggle(TextField field, Button button, Label status)
        {
            if (disposed || field.isReadOnly || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (active?.Button == button)
            {
                RequestStop(active);
                return;
            }
            Stop();

            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var exe = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(exe))
            {
                status.text = Unavailable;
                return;
            }

            var capture = new Capture
            {
                Field = field,
                Button = button,
                Status = status,
                Deadline = EditorApplication.timeSinceStartup + 12,
                Process = new Process
                {
                    StartInfo = new ProcessStartInfo(exe, WorkerArguments)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = system,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                }
            };
            capture.Process.OutputDataReceived += (_, data) => capture.Messages.Enqueue(data.Data ?? EndOfOutput);
            capture.Process.ErrorDataReceived += (_, _) => { };
            active = capture;
            try
            {
                if (!capture.Process.Start())
                    throw new InvalidOperationException("Le composant de dictée ne démarre pas.");
                capture.Process.BeginOutputReadLine();
                capture.Process.BeginErrorReadLine();
                button.text = "Arrêter la dictée";
                status.text = "Ouverture du micro…";
                EditorApplication.update += Receive;
            }
            catch (Exception exception) when (exception is Win32Exception || exception is InvalidOperationException ||
                exception is IOException || exception is UnauthorizedAccessException)
            {
                Finish(Unavailable);
            }
        }

        private void RequestStop(Capture capture)
        {
            capture.Button.SetEnabled(false);
            capture.Status.text = "Arrêt de la dictée…";
            capture.Deadline = EditorApplication.timeSinceStartup + 5;
            try { capture.Process.StandardInput.Close(); }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException)
            {
                Finish("Dictée arrêtée.");
            }
        }

        private void Receive()
        {
            var capture = active;
            if (capture == null)
                return;
            while (active == capture && capture.Messages.TryDequeue(out var message))
            {
                if (message == EndOfOutput)
                {
                    Finish(capture.Failure ?? (capture.Ready ? "Dictée terminée." : Unavailable));
                    return;
                }

                if (message == "READY")
                {
                    capture.Ready = true;
                    if (capture.Button.enabledSelf)
                    {
                        capture.Status.text = "Écoute en français…";
                        capture.Deadline = EditorApplication.timeSinceStartup + 125;
                    }
                }
                else if (message == "LANGUAGE")
                    capture.Failure = MissingFrench;
                else if (message == "ERROR")
                    capture.Failure = Unavailable;
                else if (message.StartsWith(TextPrefix, StringComparison.Ordinal))
                {
                    string phrase;
                    try { phrase = Encoding.UTF8.GetString(Convert.FromBase64String(message.Substring(TextPrefix.Length))); }
                    catch (FormatException)
                    {
                        Finish(Unavailable);
                        return;
                    }
                    string updated = AppendTranscript(capture.Field.value, phrase, capture.Field.maxLength);
                    if (updated != capture.Field.value)
                        capture.Field.value = updated;
                }
            }

            if (active == capture && EditorApplication.timeSinceStartup >= capture.Deadline)
                Finish(capture.Failure ?? (capture.Ready ? "Dictée arrêtée." : Unavailable));
        }

        internal static string AppendTranscript(string draft, string phrase, int maxLength)
        {
            draft ??= string.Empty;
            phrase = phrase?.Trim();
            if (string.IsNullOrEmpty(phrase))
                return draft;

            string separator = draft.Length == 0 || char.IsWhiteSpace(draft[draft.Length - 1]) ? "" : " ";
            if (maxLength >= 0)
            {
                int available = maxLength - draft.Length - separator.Length;
                if (available <= 0)
                    return draft;
                if (phrase.Length > available)
                {
                    if (char.IsHighSurrogate(phrase[available - 1]))
                        available--;
                    if (available == 0)
                        return draft;
                    phrase = phrase.Substring(0, available);
                }
            }

            return draft + separator + phrase;
        }

        public void Stop() => Finish("Dictée arrêtée.");

        private void Finish(string message)
        {
            var capture = active;
            if (capture == null)
                return;
            active = null;
            EditorApplication.update -= Receive;
            using (capture.Process)
            {
                try
                {
                    if (!capture.Process.HasExited)
                        capture.Process.Kill();
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is Win32Exception) { }
            }
            capture.Button.text = "Dicter en français";
            capture.Button.SetEnabled(!disposed);
            capture.Status.text = message;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                Stop();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Stop();
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }
}
