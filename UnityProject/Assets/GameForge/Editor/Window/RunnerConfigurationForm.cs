using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameForge.Runtime;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    public sealed class RunnerConfigurationForm : VisualElement
    {
        private readonly ScrollView fields = new ScrollView(ScrollViewMode.Vertical) { name = "configuration-fields" };
        private readonly HelpBox services = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = "services-status" };
        private readonly HelpBox result = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = "generation-result" };
        private readonly Dictionary<string, HelpBox> fieldErrors = new Dictionary<string, HelpBox>();
        private readonly VisualElement obstacles = new VisualElement { name = "obstacleIds" };
        private readonly List<Toggle> obstacleOptions = new List<Toggle>();
        private readonly TextField title;
        private readonly TextField seed;
        private readonly TextField initialSpeed;
        private readonly TextField maxSpeed;
        private readonly TextField acceleration;
        private readonly TextField density;
        private readonly DropdownField theme;
        private readonly Button generate;
        private IRunnerCatalog catalog;
        private IGameGenerator generator;

        public RunnerConfigurationForm()
        {
            style.flexGrow = 1f;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 8f;
            style.paddingBottom = 8f;
            fields.style.flexGrow = 1f;
            Add(services);
            Add(fields);

            var defaults = RunnerConfigV1.CreateDefault();
            var model = new DropdownField("Modèle", new List<string> { "Endless Runner 3D" }, 0) { name = "templateId" };
            model.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            fields.Add(model);
            title = AddField(new TextField("Titre") { value = defaults.title }, "title");
            seed = AddField(new TextField("Graine") { value = Number(defaults.seed) }, "seed");
            initialSpeed = AddField(new TextField("Vitesse initiale (m/s)") { value = Number(defaults.initialSpeed) }, "initialSpeed");
            maxSpeed = AddField(new TextField("Vitesse maximale (m/s)") { value = Number(defaults.maxSpeed) }, "maxSpeed");
            acceleration = AddField(new TextField("Accélération (m/s²)") { value = Number(defaults.acceleration) }, "acceleration");
            density = AddField(new TextField("Obstacles par segment") { value = Number(defaults.obstacleDensity) }, "obstacleDensity");
            theme = AddField(new DropdownField("Thème"), "themeId");
            fields.Add(new Label("Obstacles autorisés"));
            fields.Add(obstacles);
            AddErrorBox("obstacleIds");
            AddErrorBox("$");

            generate = new Button(() => TryGenerate()) { text = "Générer", name = "generate" };
            Add(result);
            Add(generate);
            Bind(null, null, null, null);
        }

        public void Bind(IRunnerCatalog runnerCatalog, IReadOnlyList<string> themeIds,
            IReadOnlyList<string> obstacleIds, IGameGenerator gameGenerator)
        {
            var firstCatalog = catalog == null;
            catalog = runnerCatalog;
            generator = gameGenerator;

            var defaults = RunnerConfigV1.CreateDefault();
            var choices = DistinctIds(themeIds);
            var preferred = choices.Contains(defaults.themeId) ? defaults.themeId : choices.FirstOrDefault();
            var selected = choices.Contains(theme.value) ? theme.value : preferred;
            theme.choices = choices;
            theme.SetValueWithoutNotify(selected ?? string.Empty);
            theme.SetEnabled(choices.Count > 0);

            var checkedIds = new HashSet<string>(SelectedObstacles(), StringComparer.Ordinal);
            var availableObstacles = DistinctIds(obstacleIds);
            if (firstCatalog && catalog != null && availableObstacles.Count > 0)
                checkedIds.Add(availableObstacles.Contains(defaults.obstacleIds[0]) ? defaults.obstacleIds[0] : availableObstacles[0]);

            obstacles.Clear();
            obstacleOptions.Clear();
            foreach (var id in availableObstacles)
            {
                var option = new Toggle(id) { name = "obstacle-" + id, value = checkedIds.Contains(id) };
                option.RegisterValueChangedCallback(_ => InputChanged());
                obstacleOptions.Add(option);
                obstacles.Add(option);
            }

            InputChanged();
        }

        public bool TryGenerate()
        {
            result.style.display = DisplayStyle.None;
            if (!Validate(out var configuration))
                return false;

            try
            {
                var response = generator.Generate(configuration);
                if (response?.Succeeded == true)
                {
                    ShowResult(HelpBoxMessageType.Info, $"Variante {response.VariantId} créée dans {response.ScenePath}.");
                    return true;
                }

                var messages = response?.Diagnostics?.Select(diagnostic => diagnostic.Message);
                ShowResult(HelpBoxMessageType.Error, messages == null || !messages.Any()
                    ? "La génération a échoué."
                    : string.Join("\n", messages));
            }
            catch (Exception exception)
            {
                ShowResult(HelpBoxMessageType.Error, $"La génération a échoué. {exception.Message}");
            }

            return false;
        }

        private T AddField<T>(T field, string name) where T : BaseField<string>
        {
            field.name = name;
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            fields.Add(field);
            AddErrorBox(name);
            field.RegisterValueChangedCallback(_ => InputChanged());
            return field;
        }

        private void AddErrorBox(string name)
        {
            var box = new HelpBox(string.Empty, HelpBoxMessageType.Error) { name = name + "-error" };
            box.style.display = DisplayStyle.None;
            fields.Add(box);
            fieldErrors.Add(name, box);
        }

        private void InputChanged()
        {
            result.style.display = DisplayStyle.None;
            Validate(out _);
        }

        private bool Validate(out RunnerConfigV1 configuration)
        {
            var issues = new List<RunnerDiagnostic>();
            configuration = new RunnerConfigV1
            {
                schemaVersion = RunnerConfigV1.Version,
                templateId = RunnerConfigV1.RunnerTemplateId,
                title = title.value,
                seed = ReadInteger(seed, issues),
                initialSpeed = ReadNumber(initialSpeed, issues),
                maxSpeed = ReadNumber(maxSpeed, issues),
                acceleration = ReadNumber(acceleration, issues),
                obstacleDensity = ReadInteger(density, issues),
                themeId = theme.value,
                obstacleIds = SelectedObstacles()
            };

            if (catalog != null)
            {
                var syntaxFields = new HashSet<string>(issues.Select(issue => issue.Field), StringComparer.Ordinal);
                issues.AddRange(RunnerConfigValidator.Validate(configuration, catalog)
                    .Where(issue => !syntaxFields.Contains(issue.Field)));
            }

            foreach (var box in fieldErrors.Values)
            {
                box.text = string.Empty;
                box.style.display = DisplayStyle.None;
            }

            foreach (var issue in issues)
            {
                var field = issue.Field.StartsWith("obstacleIds[", StringComparison.Ordinal) ? "obstacleIds" : issue.Field;
                var box = fieldErrors.TryGetValue(field, out var found) ? found : fieldErrors["$"];
                box.text = string.IsNullOrEmpty(box.text) ? issue.Message : box.text + "\n" + issue.Message;
                box.style.display = DisplayStyle.Flex;
            }

            services.text = catalog == null ? "Raccorder le catalogue et le générateur GameForge pour générer une variante." :
                generator == null ? "Raccorder le générateur GameForge pour créer une variante." : string.Empty;
            services.style.display = string.IsNullOrEmpty(services.text) ? DisplayStyle.None : DisplayStyle.Flex;
            var ready = catalog != null && generator != null && issues.Count == 0;
            generate.SetEnabled(ready);
            return ready;
        }

        private void ShowResult(HelpBoxMessageType type, string message)
        {
            result.messageType = type;
            result.text = message;
            result.style.display = DisplayStyle.Flex;
        }

        private string[] SelectedObstacles() => obstacleOptions.Where(option => option.value).Select(option => option.label).ToArray();

        private static List<string> DistinctIds(IReadOnlyList<string> ids) => (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();

        private static string Number(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

        private static int ReadInteger(TextField input, List<RunnerDiagnostic> issues)
        {
            if (int.TryParse(input.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return value;

            issues.Add(new RunnerDiagnostic(input.name, "Entier signé sur 32 bits requis."));
            return 0;
        }

        private static float ReadNumber(TextField input, List<RunnerDiagnostic> issues)
        {
            if (float.TryParse((input.value ?? string.Empty).Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value) &&
                !float.IsInfinity(value) && !float.IsNaN(value))
            {
                return value;
            }

            issues.Add(new RunnerDiagnostic(input.name, "Nombre fini requis."));
            return 0f;
        }
    }
}
