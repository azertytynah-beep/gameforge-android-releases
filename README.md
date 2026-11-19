# GameForge AI

## Installer le test Android

1. Sur le Redmi, [télécharge l’APK de test](https://github.com/azertytynah-beep/gameforge-android-releases/releases/latest/download/gameforge-android-test.apk).
2. Ouvre le fichier téléchargé. Si Android le demande, autorise temporairement ton navigateur à installer cette application, puis confirme l’installation.
3. Ouvre **GameForge AI**. La première version affiche un écran de validation de la chaîne Android. Le Runner jouable viendra dans une prochaine publication.

Si le téléchargement direct reste bloqué, télécharge l’archive ZIP dans la publication, ouvre-la depuis le gestionnaire de fichiers du téléphone, puis extrais et ouvre l’APK. [Archive ZIP de la version de validation](https://github.com/azertytynah-beep/gameforge-android-releases/releases/download/android-test-v0.1.0/gameforge-android-test.zip)

Le même lien télécharge la dernière version publiée. Pour les versions et leurs notes, consulte [les publications GitHub](https://github.com/azertytynah-beep/gameforge-android-releases/releases). Dès qu’une série utilise le même identifiant et la même signature, Android peut installer une version plus récente par-dessus la précédente.

## Ouvrir le projet Unity

Dans Unity Hub, ouvre `UnityProject/` avec Unity 6.3 LTS `6000.3.28f1`. Pour compiler sur Android, ajoute Android Build Support, Android SDK & NDK Tools et OpenJDK depuis Unity Hub.

Pour essayer le Runner dans l’éditeur, ouvre `Assets/GameForge/Content/Templates/Runner/RunnerBase.unity`, puis lance Play.
