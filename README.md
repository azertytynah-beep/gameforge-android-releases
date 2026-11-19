# GameForge AI

## Installer le test Android

1. Sur le Redmi, [télécharge le ZIP du Runner](https://github.com/azertytynah-beep/gameforge-android-releases/releases/latest/download/gameforge-runner-android.zip).
2. Dans le gestionnaire de fichiers, extrais le ZIP puis ouvre `gameforge-runner.apk` et confirme l’installation.
3. Ouvre **GameForge Runner** et appuie sur **Jouer**. Le Runner s’installe à côté du premier test Android au fond bleu.

Le ZIP suit la dernière publication du Runner. Chaque version propose également l’APK seul. [Versions et notes de publication sur GitHub](https://github.com/azertytynah-beep/gameforge-android-releases/releases)

## Ouvrir le projet Unity

Dans Unity Hub, ouvre `UnityProject/` avec Unity 6.3 LTS `6000.3.28f1`. Pour compiler sur Android, ajoute Android Build Support, Android SDK & NDK Tools et OpenJDK depuis Unity Hub.

Dans **Tools > GameForge**, clique sur **Commencer à créer un jeu**. Tu peux donner un nom dès le départ ou retrouver le brouillon **Sans titre** dans **Mes jeux**. Décris ton idée, demande des suggestions ou choisis le modèle **Runner 3D**. Réponds au questionnaire, puis choisis parmi quatre maquettes d’apparence et quatre façons de jouer. Chaque série peut être régénérée. Verrouille les réponses que tu veux garder dans toutes les propositions.

La scène jouable est créée quand tu confirmes le gameplay. Dans **Variantes**, utilise **Prévisualiser** pour jouer dans Unity ou **Ouvrir** pour modifier la scène. Enregistre la scène avant de sauvegarder ou dupliquer ta variante. **Ajuster les réglages** reste accessible pour créer directement une variante. Le menu **Apparence** propose **Clair**, **Sombre** et le thème de Unity.

La recherche d’idées fonctionne hors ligne. Pour demander des idées à Ollama, renseigne le nom d’un modèle local déjà installé dans **Trouver une idée > Configurer l’IA locale**. Les brouillons sont enregistrés sur le poste dans `.local/projects/`.

Pour obtenir un APK, active **Android** dans **File > Build Profiles > Platforms**, sélectionne une variante enregistrée, puis utilise **Exporter > Exporter l’APK** dans GameForge. Unity écrit l’APK et son rapport dans `UnityProject/Builds/GameForge/`. [Procédure d’export](Docs/ExportAPK.md)
