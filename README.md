# GameForge AI

## Studio Windows

Le Studio prépare le jeu dans une application Windows autonome. **Commencer à créer un jeu** ouvre un brouillon. Le questionnaire, la dictée, les quatre apparences et les quatre gameplays fonctionnent depuis cette fenêtre. **Créer ce jeu avec Unity** démarre Unity 6.3 LTS en arrière-plan. Le résultat propose **Jouer sur PC** et **Exporter l’APK de test**.

Compare les ambiances **Orbital** et **Relief**, disponibles en clair et en sombre. Après extraction du dépôt, ouvre [la galerie des quatre maquettes](Desktop/Apercus/index.html) dans ton navigateur. Le réglage **Réduire les animations** immobilise les fonds du Studio.

Les nouveaux jeux sont conservés dans le dossier `GameForge AI/Atelier` des données locales de l’utilisateur Windows. **Réglages → Importer un ancien brouillon** copie un projet de l’atelier Unity. Une scène déjà générée peut être recréée depuis le gameplay importé.

Le MSI reste en validation. Une mise en quarantaine par Malwarebytes lors de l’essai sur Windows empêche pour le moment sa distribution. Les sources du Studio et les maquettes sont disponibles dans `Desktop/`.

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
