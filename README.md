# GameForge AI

## Studio Windows

Le Studio prépare le jeu dans une application Windows autonome. **Commencer à créer un jeu** ouvre un brouillon. Le questionnaire, la dictée, les quatre apparences et les quatre gameplays fonctionnent depuis cette fenêtre. **Créer ce jeu avec Unity** démarre Unity 6.3 LTS en arrière-plan. Le résultat propose **Jouer sur PC** et **Exporter l’APK de test**.

**Prisme** est l’accueil par défaut du Studio. La navigation se trouve en haut, **Créer un jeu** ouvre le brief et les projets récents sont accessibles sous la sculpture irisée. Le menu **Ambiances** conserve Orbital et Relief et permet de réduire les animations. [Rendu clair](Desktop/Apercus/Prisme-clair.png) · [Rendu sombre](Desktop/Apercus/Prisme-sombre.png)

Une aura discrète accompagne le survol des boutons et des onglets. Elle respecte la réduction des animations et le contraste élevé. La barre de défilement est masquée ; le contenu reste défilable. [Comparer les trois maquettes du questionnaire](Desktop/Apercus/questionnaires.html), en clair et sombre. Ces maquettes illustrent le futur questionnaire, leur micro est une démonstration sans enregistrement.

**Dictée Windows · Win + H** prépare le champ sans enregistrer : appuie ensuite sur **Windows + H** pour utiliser la saisie vocale de Windows 11. Elle nécessite Internet, traite l'audio chez Microsoft et utilise la langue de saisie Windows. Arrête-la depuis son panneau avant de changer de champ. L'ancienne dictée locale reste explicitement expérimentale après le signalement de transcriptions erronées. La précision du nouveau parcours doit être essayée avec le micro de l'utilisateur. [Fonctionnement de la saisie vocale Windows](https://support.microsoft.com/fr-fr/accessibility/windows/use-voice-typing-to-talk-instead-of-type-on-your-pc)

Les nouveaux jeux sont conservés dans le dossier `GameForge AI/Atelier` des données locales de l’utilisateur Windows. **Réglages → Importer un ancien brouillon** copie un projet de l’atelier Unity. Une scène déjà générée peut être recréée depuis le gameplay importé.

Le MSI et le ZIP portable restent en validation. Le lancement de l'exécutable Prisme extrait du ZIP a reproduit une mise en quarantaine Malwarebytes. La cause est en attente d'analyse. Les sources du Studio et les maquettes sont disponibles dans `Desktop/`. La commande `Desktop/Package.ps1 -Build <dossier_compilé> -Portable` prépare le ZIP avec les cinq fichiers nécessaires et sa notice. Cette archive ne constitue pas encore une livraison validée.

## Installer le test Android

1. Sur le Redmi, [télécharge le ZIP du Runner](https://github.com/azertytynah-beep/gameforge-android-releases/releases/latest/download/gameforge-runner-android.zip).
2. Dans le gestionnaire de fichiers, extrais le ZIP puis ouvre `gameforge-runner.apk` et confirme l’installation.
3. Ouvre **GameForge Runner** et appuie sur **Jouer**. Le Runner s’installe à côté du premier test Android au fond bleu.

Le ZIP suit la dernière publication du Runner. Chaque version propose également l’APK seul. [Versions et notes de publication sur GitHub](https://github.com/azertytynah-beep/gameforge-android-releases/releases)

## Ouvrir le projet Unity

Dans Unity Hub, ouvre `UnityProject/` avec Unity 6.3 LTS `6000.3.28f1`. Pour compiler sur Android, ajoute Android Build Support, Android SDK & NDK Tools et OpenJDK depuis Unity Hub.

Dans **Tools > GameForge**, clique sur **Commencer à créer un jeu**. Tu peux donner un nom dès le départ ou retrouver le brouillon **Sans titre** dans **Mes jeux**. Décris ton idée, demande des suggestions ou choisis le modèle **Runner 3D**.

Le questionnaire de l’**Atelier** se parcourt en cinq chapitres. Décris par exemple l’univers, la silhouette des ennemis, le rythme, la victoire et les besoins de confort. Clique sur **Dicter en français** pour ajouter ta voix à un champ sous Windows et corrige le texte avant de continuer. Tu peux verrouiller tes réponses ou laisser GameForge proposer. Le brouillon et le chapitre courant se sauvegardent au fil de la saisie.

Choisis parmi quatre maquettes d’apparence et quatre façons de jouer, avec une nouvelle série à la demande. Les maquettes présentent la palette et une silhouette d’ennemi. Le Runner peut construire localement des silhouettes low-poly originales de robot, drone, créature, golem ou plante, à côté des obstacles de base. Le gameplay précise la tactique, les obstacles et les conditions de fin de partie.

La scène jouable est créée quand tu confirmes le gameplay. Dans **Variantes**, utilise **Prévisualiser** pour jouer dans Unity ou **Ouvrir** pour modifier la scène. Enregistre la scène avant de sauvegarder ou dupliquer ta variante. **Ajuster les réglages** reste accessible pour créer directement une variante. Le menu **Apparence** propose **Clair**, **Sombre** et le thème de Unity.

La recherche d’idées fonctionne hors ligne. Pour demander des idées à Ollama, renseigne le nom d’un modèle local déjà installé dans **Trouver une idée > Configurer l’IA locale**. Les brouillons sont enregistrés sur le poste dans `.local/projects/`.

Pour utiliser OmniRoute, renseigne l’adresse de ta passerelle jusqu’à `/v1` et le nom du modèle dans **Configurer OmniRoute**. La clé `GAMEFORGE_OMNIROUTE_API_KEY` doit être disponible dans l’environnement de la session Unity. **Proposer avec OmniRoute** envoie le brief au modèle configuré. Tu peux annuler la demande ou utiliser la génération locale. [Documentation OmniRoute](https://github.com/cheaper-inference/OmniRoute/blob/release/v3.8.54/docs/reference/OPENAI_COMPATIBILITY.md)

Pour obtenir un APK, active **Android** dans **File > Build Profiles > Platforms**, sélectionne une variante enregistrée, puis utilise **Exporter > Exporter l’APK** dans GameForge. Unity écrit l’APK et son rapport dans `UnityProject/Builds/GameForge/`. [Procédure d’export](Docs/ExportAPK.md)
