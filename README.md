# GameForge AI

## Installer le test Android

1. Sur le Redmi, [télécharge le ZIP du Runner](https://github.com/azertytynah-beep/gameforge-android-releases/releases/latest/download/gameforge-runner-android.zip).
2. Dans le gestionnaire de fichiers, extrais le ZIP puis ouvre `gameforge-runner.apk` et confirme l’installation.
3. Ouvre **GameForge Runner** et appuie sur **Jouer**. Le Runner s’installe à côté du premier test Android au fond bleu.

Le ZIP suit la dernière publication du Runner. Chaque version propose également l’APK seul. [Versions et notes de publication sur GitHub](https://github.com/azertytynah-beep/gameforge-android-releases/releases)

## Ouvrir le projet Unity

Dans Unity Hub, ouvre `UnityProject/` avec Unity 6.3 LTS `6000.3.28f1`. Pour compiler sur Android, ajoute Android Build Support, Android SDK & NDK Tools et OpenJDK depuis Unity Hub.

Dans **Tools > GameForge**, donne un nom à ton jeu dans **Décrire**. Passe à **Ajuster** pour choisir le thème, les vitesses et les obstacles, puis clique sur **Générer la variante**. Dans **Variantes**, utilise **Prévisualiser** pour jouer dans Unity ou **Ouvrir** pour modifier la scène. Enregistre la scène avant de sauvegarder ou dupliquer ta variante. Le menu **Apparence** permet de suivre le thème de Unity ou de choisir **Clair** ou **Sombre**.

Pour obtenir un APK, active **Android** dans **File > Build Profiles > Platforms**, sélectionne une variante enregistrée, puis utilise **Exporter > Exporter l’APK** dans GameForge. Unity écrit l’APK et son rapport dans `UnityProject/Builds/GameForge/`.

Pour essayer le Runner dans l’éditeur, ouvre `Assets/GameForge/Content/Templates/Runner/RunnerBase.unity`, puis lance Play.
