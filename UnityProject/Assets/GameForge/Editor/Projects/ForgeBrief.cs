using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameForge.Editor
{
    internal enum ForgePromptId
    {
        Concept, Player, Audience, World, Art, Atmosphere, Readability,
        Enemy, EnemyBehavior, Encounters, AssetSource,
        Controls, Goal, Failure, Progression, Audio, Accessibility
    }

    internal sealed class ForgeBriefPrompt
    {
        public ForgePromptId Id { get; }
        public string Question { get; }
        public string Example { get; }
        public bool Required { get; }

        public ForgeBriefPrompt(ForgePromptId id, string question, string example, bool required = false)
        {
            Id = id;
            Question = question;
            Example = example;
            Required = required;
        }
    }

    internal sealed class ForgeBriefChapter
    {
        public string Title { get; }
        public IReadOnlyList<ForgeBriefPrompt> Prompts { get; }

        public ForgeBriefChapter(string title, params ForgeBriefPrompt[] prompts)
        {
            Title = title;
            Prompts = prompts;
        }
    }

    [Serializable]
    internal sealed class ForgeBrief
    {
        public const int AnswerLimit = 2300;
        internal const string DelegateAnswer = "Je laisse GameForge proposer.";
        public static readonly ForgeBriefChapter[] Chapters =
        {
            new("Intention",
                new(ForgePromptId.Concept, "Que fait le joueur au cours d’une partie typique ?", "Une livreuse traverse une ville flottante pour remettre un colis avant la nuit.", true),
                new(ForgePromptId.Player, "Qui incarne-t-on et comment doit-on le reconnaître de dos ?", "Un petit renard avec une combinaison orange et un sac carré.", true),
                new(ForgePromptId.Audience, "À qui s’adresse le jeu et combien de temps devrait durer une partie ?", "Pour toute la famille, une partie de 30 à 90 secondes.")),
            new("Univers visuel",
                new(ForgePromptId.World, "Où se déroule la course ? Que voit-on au premier plan et à l’horizon ?", "Une forêt sous la pluie, des troncs près de la piste et des ruines au loin.", true),
                new(ForgePromptId.Art, "Quel style et quelles couleurs veux-tu pour le jeu ?", "Des formes low-poly, du violet pour le ciel et de l’ambre pour les dangers."),
                new(ForgePromptId.Atmosphere, "Quelle heure, quelle météo et quelle ambiance la scène doit-elle évoquer ?", "Une aube calme avec des reflets froids et une piste lumineuse."),
                new(ForgePromptId.Readability, "Quelles formes ou couleurs doivent permettre de distinguer la piste, le héros et le danger ?", "La piste reste mate, le héros a un contour clair et les ennemis contrastent avec le sol.")),
            new("Ennemis et obstacles",
                new(ForgePromptId.Enemy, "Quel type d’ennemi veux-tu ? Décris sa silhouette, sa taille et ses couleurs.", "Un drone vert avec deux rotors, assez petit pour occuper une seule voie.", true),
                new(ForgePromptId.EnemyBehavior, "Que fait l’ennemi quand le joueur approche et comment annonce-t-il le danger ?", "Il surveille une voie. Une lumière permet de le repérer de loin."),
                new(ForgePromptId.Encounters, "Comment les dangers doivent-ils être répartis ? Décris un passage facile puis un passage difficile.", "D’abord un seul obstacle visible, puis des groupes qui obligent à changer de voie."),
                new(ForgePromptId.AssetSource, "Quels éléments 3D sont indispensables et d’où veux-tu les obtenir ?", "Un robot original créé pour ce jeu, ou un modèle gratuit dont la redistribution est autorisée.")),
            new("Règles du jeu",
                new(ForgePromptId.Controls, "Que doit faire le joueur avec les commandes gauche et droite ?", "Anticiper deux changements de voie pour traverser un groupe de barrières."),
                new(ForgePromptId.Goal, "Qu’est-ce qui fait gagner ou donne envie d’améliorer son score ?", "Atteindre 150 mètres avant la fin du chrono, ou battre sa meilleure distance.", true),
                new(ForgePromptId.Failure, "Quand doit-on perdre et que doit-il se passer juste après ?", "Le contact avec un ennemi met fin à la course. On rejoue immédiatement."),
                new(ForgePromptId.Progression, "Comment la difficulté doit-elle évoluer entre le début et la fin de la partie ?", "Une introduction tranquille, puis davantage de changements de voie à mesure que la vitesse augmente.")),
            new("Confort et finition",
                new(ForgePromptId.Audio, "Quels sons ou signaux aideraient à comprendre ce qui arrive ?", "Un signal discret au départ et un son clair quand le temps est presque écoulé."),
                new(ForgePromptId.Accessibility, "Quels besoins de lisibilité, de confort ou d’accessibilité faut-il prendre en compte ?", "Boutons grands et espacés, contraste marqué et aucune lumière clignotante."))
        };

        public bool detailed;
        public ForgeBriefEntry[] entries = Array.Empty<ForgeBriefEntry>();

        public string Get(ForgePromptId id) => Find(id)?.text ?? "";
        public bool IsLocked(ForgePromptId id) => Find(id)?.locked == true;
        public int MissingChapter => Array.FindIndex(Chapters, chapter => chapter.Prompts.Any(prompt =>
            prompt.Required && string.IsNullOrWhiteSpace(Get(prompt.Id))));

        public void Set(ForgePromptId id, string text)
        {
            var entry = FindOrAdd(id);
            entry.text = text ?? "";
        }

        public void SetLocked(ForgePromptId id, bool locked) => FindOrAdd(id).locked = locked;

        public void Validate()
        {
            var seen = new HashSet<ForgePromptId>();
            if (entries == null)
                throw new InvalidDataException("Le questionnaire du projet est illisible.");
            foreach (var entry in entries)
            {
                if (entry == null || !Enum.IsDefined(typeof(ForgePromptId), entry.id) || !seen.Add(entry.id) ||
                    entry.text?.Length > AnswerLimit || (entry.text != null &&
                        entry.text.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')))
                    throw new InvalidDataException("Une réponse du questionnaire dépasse la limite ou contient des caractères invalides.");
            }
        }

        private ForgeBriefEntry Find(ForgePromptId id) => Array.Find(entries, entry => entry.id == id);

        private ForgeBriefEntry FindOrAdd(ForgePromptId id)
        {
            detailed = true;
            var entry = Find(id);
            if (entry == null)
            {
                entry = new ForgeBriefEntry { id = id };
                Array.Resize(ref entries, entries.Length + 1);
                entries[entries.Length - 1] = entry;
            }
            return entry;
        }
    }

    [Serializable]
    internal sealed class ForgeBriefEntry
    {
        public ForgePromptId id;
        public string text;
        public bool locked;
    }
}
