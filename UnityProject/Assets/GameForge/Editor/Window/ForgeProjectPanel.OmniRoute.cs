using System;
using System.IO;
using System.Threading;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    internal sealed partial class ForgeProjectPanel
    {
        private void AddOmniRoute(bool gameplay, bool regenerate)
        {
            var settings = new Foldout { name = "forge-project-omni-settings", text = "Configurer OmniRoute", value = false };
            settings.AddToClassList("gameforge-atelier-settings");
            var endpoint = new TextField("Adresse OmniRoute") { name = "forge-project-omni-url", maxLength = 256, isDelayed = true };
            endpoint.SetValueWithoutNotify(service.OmniUrl);
            var model = new TextField("Modèle") { name = "forge-project-omni-model", maxLength = 128, isDelayed = true };
            model.SetValueWithoutNotify(service.OmniModel);
            settings.Add(endpoint);
            settings.Add(model);
            settings.Add(Label("GameForge lit la clé d’accès dans GAMEFORGE_OMNIROUTE_API_KEY.", "gameforge-caption"));
            page.Add(settings);

            const string disclosure = "Envoie les réponses du brief au modèle choisi.";
            var send = Button("forge-project-propose-omni", "Proposer avec OmniRoute", () => RequestOmni(gameplay, regenerate));
            send.tooltip = disclosure;
            var remoteAction = Element("gameforge-atelier-omni-action");
            remoteAction.Add(send);
            remoteAction.Add(Label(disclosure, "gameforge-atelier-disclosure"));
            footer.Add(remoteAction);
            send.SetEnabled(service.HasOmniConfiguration);

            endpoint.RegisterValueChangedCallback(change => Perform(() =>
            {
                service.OmniUrl = change.newValue.Trim();
                send.SetEnabled(service.HasOmniConfiguration);
            }));
            model.RegisterValueChangedCallback(change => Perform(() =>
            {
                service.OmniModel = change.newValue.Trim();
                send.SetEnabled(service.HasOmniConfiguration);
            }));
        }

        private async void RequestOmni(bool gameplay, bool regenerate)
        {
            if (omniRequest != null || (!gameplay && !regenerate && FindMissingAnswer()))
                return;

            var request = new CancellationTokenSource();
            omniRequest = request;
            dictation?.Stop();
            SetOmniBusy(true);
            Report(gameplay ? "OmniRoute prépare les propositions de gameplay…" : "OmniRoute prépare les maquettes…",
                HelpBoxMessageType.Info);

            string failure = null;
            bool cancelled = false;
            try
            {
                if (gameplay)
                    await service.ProposeGameplayWithAiAsync(project, regenerate, request.Token);
                else
                    await service.ProposeLooksWithAiAsync(project, regenerate, request.Token);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { cancelled = true; }
            catch (TimeoutException) { failure = "OmniRoute a dépassé le délai de réponse."; }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or
                InvalidOperationException or UnauthorizedAccessException)
            {
                failure = exception.Message;
            }
            catch (Exception)
            {
                failure = "La demande à OmniRoute a échoué.";
            }
            finally
            {
                if (omniRequest == request)
                {
                    omniRequest = null;
                    SetOmniBusy(false);
                    if (failure != null)
                        Report(failure + " Les propositions locales sont disponibles.");
                    else if (cancelled)
                        Report("Demande annulée. Tu peux continuer avec les propositions locales.", HelpBoxMessageType.Info);
                    else
                    {
                        ClearStatus();
                        ShowStage();
                    }
                }
                request.Dispose();
            }
        }

        private void SetOmniBusy(bool busy)
        {
            header.SetEnabled(!busy);
            page.SetEnabled(!busy);
            footer.SetEnabled(!busy);
            cancelOmni.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateSteps();
        }

        private void AbandonOmniRequest()
        {
            if (omniRequest == null)
                return;
            var request = omniRequest;
            omniRequest = null;
            request.Cancel();
            SetOmniBusy(false);
        }
    }
}
