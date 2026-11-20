# Questionnaire de conception proposé

Analyse du 20 novembre 2026. Proposition à valider avant modification du formulaire ou des données des projets. Les questions ci-dessous sont une synthèse de conception GameForge, les sources officielles étayent les exigences Android et Google Play citées.

La disposition **Focus** est maintenant choisie et intégrée au Studio Windows. Elle présente une question à la fois et enregistre la position exacte dans le brief. Les 17 questions actuelles sont conservées. Le noyau et les branches détaillés ci-dessous restent une proposition d'évolution du contenu, distincte de ce choix visuel.

Le bon objectif est un brief assez précis pour produire puis tester une première version jouable. Le nombre de réponses ne garantit ni la qualité du jeu ni son acceptation sur Google Play. Android distingue la valeur pour le joueur, l'expérience, la qualité technique et la sécurité. Le questionnaire doit préparer ces quatre dimensions. [Qualité Android](https://developer.android.com/quality)

## État des 17 questions actuelles

Leur texte est défini dans `ForgeBrief.cs`. Cinq réponses sont requises, les autres restent facultatives. Les exemples et les commandes présupposent le Runner à trois voies.

| Question actuelle | Apport conservé | Précision manquante |
|---|---|---|
| Concept | Partie typique | Action répétée, décision intéressante, première minute |
| Player | Silhouette reconnaissable | Capacités, limites, caméra adaptée au genre |
| Audience | Public et durée | Âges précis, niveau d'expérience, durée séparée du public |
| World | Décor et horizon | Zones explorables, limites du terrain, interactions |
| Art | Style et couleurs | Références autorisées, priorité entre fidélité et fluidité |
| Atmosphere | Heure et météo | Ambiance sonore, effets à éviter |
| Readability | Héros et dangers distincts | Signaux indépendants de la couleur, lecture petit écran |
| Enemy | Silhouette d'ennemi | Possibilité explicite sans ennemis, variété nécessaire |
| EnemyBehavior | Réaction et annonce | États, portée, vitesse relative, contre-jeu |
| Encounters | Exemple facile/difficile | Règle de placement, hasard, passages impossibles |
| AssetSource | Origine des modèles | Animation, sons, polices, preuve de droits et attribution |
| Controls | Gauche/droite | Gestes selon le genre, pause, tutoriel, retour visuel |
| Goal | Victoire ou score | Score calculé, condition observable de réussite |
| Failure | Défaite et reprise | Vie, checkpoint, contenu conservé après échec |
| Progression | Difficulté dans une partie | Déblocages entre parties, niveaux, quantité de contenu |
| Audio | Signaux utiles | Musique, voix, volumes séparés, option muet |
| Accessibility | Lisibilité et confort | Alternatives tactiles/sonores, mouvements, réglages |

Les lacunes principales sont la portée de la première version, les états de partie, les interactions propres au genre, la sauvegarde, le fonctionnement hors ligne et les choix qui affectent la publication.

## Parcours proposé

Présenter une question centrale à la fois, une grande réponse libre et une aide dépliable. La navigation par chapitre reste disponible. Enregistrer le brouillon, le curseur de progression et les décisions validées. Le récapitulatif permet de corriger les réponses sans recommencer.

Chaque réponse accepte le clavier et la dictée. « Aide-moi à choisir » propose une réponse modifiable. « Pas encore décidé » garde un point ouvert visible. Éviter une obligation de remplir artificiellement tous les champs. Une réponse déléguée doit devenir une hypothèse concrète dans le récapitulatif avant génération.

Parcours **Essentiel** pour la première version et **Approfondir** pour les détails. Une demande complexe déclenche une branche ciblée et une révision de faisabilité. Les branches sans rapport avec le jeu restent masquées. Le nom du jeu reste facultatif.

## Noyau de questions, avec sous-questions progressives

| Étape | Question principale précise | Aide et résultat attendu |
|---|---|---|
| 1 · Promesse | Dans une partie, que fait le joueur et quelle décision rend cela amusant ? | « J'esquive des zombies en choisissant le bon couloir. » Décrire 30 secondes de jeu. |
| 2 · Joueur | Qui contrôle-t-on, que peut-il faire et quelles sont ses limites ? | Courir automatiquement, changer de voie, aucune attaque. Pour un puzzle, décrire plutôt les objets manipulés. |
| 3 · Public | À quels âges et à quels joueurs t'adresses-tu ? | Âges envisagés, débutants ou habitués, besoins particuliers. Ne pas se contenter de « tout public ». |
| 4 · Session | Combien de temps dure une partie et comment commence-t-elle ? | Courte session, explication des commandes, première situation facile. |
| 5 · Commandes | Quels gestes permettent chaque action et comment le joueur voit-il qu'elle a réussi ? | Boutons, glissement ou appui selon le jeu. Pause et reprise, retour visuel/sonore. |
| 6 · Réussite | Quelle situation exacte fait gagner, et comment calcule-t-on le score ? | Atteindre 150 m, terminer un puzzle, survivre 60 s. Préciser une valeur ou laisser proposer. |
| 7 · Échec | Qu'est-ce qui fait perdre, et que conserve-t-on quand on rejoue ? | Un contact, plusieurs vies ou aucun échec. Redémarrage, checkpoint, score ou objets conservés. |
| 8 · Défi | Décris une situation facile, puis une situation difficile qui reste faisable. | Distance de réaction, ressources, temps, combinaisons. GameForge traduit ensuite en paramètres testables. |
| 9 · Monde | Où joue-t-on, que peut-on toucher ou traverser, et comment la caméra montre-t-elle l'action ? | Décor de fond ou terrain interactif, limites, vue portrait/paysage. |
| 10 · Identité | Quel aspect et quelle ambiance veux-tu reconnaître immédiatement ? | Style, palette, lumière, références visuelles, sons. Séparer préférences et éléments indispensables. |
| 11 · Continuité | Que doit-on retrouver après avoir fermé le jeu, et que doit-on pouvoir faire sans Internet ? | Meilleur score seul, partie en cours, niveaux débloqués, aucune sauvegarde. |
| 12 · Périmètre | Quels sont les trois éléments indispensables à la première version, et comment sauras-tu qu'elle est réussie ? | Exemple de recette jouable et exclusions explicites. Repérer multijoueur, boutique ou autre besoin qui change l'architecture. |

Confort, langue, provenance des ressources, réseau et modèle économique font l'objet de confirmations courtes avant le récapitulatif. Les détails ci-dessous apparaissent seulement si utiles. Ce noyau est une proposition de parcours, pas douze champs qui absorbent toutes les précisions.

## Branches conditionnelles

| Déclencheur | Questions complémentaires concrètes |
|---|---|
| Ennemis ou obstacles | Quels types, tailles et silhouettes ? Que font-ils avant/pendant/après une rencontre ? Quel signal annonce leur action ? Comment les éviter ou les vaincre ? Combien de types sont indispensables ? |
| Combat | Quelles attaques, cadence, dégâts, portée et défenses ? Comment lit-on la santé ? Donner un affrontement complet. |
| Puzzle | Quel état initial, quelles actions autorisées et quelle solution valide ? Comment éviter un blocage sans solution ? Aide, annulation ou remise à zéro ? |
| Niveaux ou progression | Combien de niveaux pour le premier test ? Qu'est-ce qui change ? Quels déblocages, récompenses et contenus répétés ? |
| Économie de jeu | Que gagne, dépense ou échange le joueur ? Quelles quantités et limites ? Est-ce lié à de l'argent réel ? |
| Sauvegarde | Quand sauvegarder ? Localement ou entre appareils ? Que se passe-t-il après interruption ou conflit ? Un compte est-il réellement nécessaire ? |
| Multijoueur | Combien de joueurs, ensemble ou chacun son tour ? Invitations, déconnexion, équité, hôte/serveur ? Qui finance et entretient le service ? |
| Compte, chat ou contenu partagé | Qui peut voir quoi ? Comment signaler, bloquer et modérer ? Comment supprimer le compte et les données ? Les fonctions communautaires impliquent une modération adaptée. [Contenus utilisateurs](https://support.google.com/googleplay/android-developer/answer/18265346?hl=en-GB) |
| Ressources externes ou IA 3D | Quelles ressources exactes, animations et formats ? Quelle source et quelle preuve de droits ? Quel remplacement si une ressource manque ? Gratuit ne signifie pas librement redistribuable. [Droits de propriété](https://support.google.com/googleplay/android-developer/answer/9888072?hl=en) |
| Accessibilité et audio | Comment jouer sans son, sans distinguer certaines couleurs ou avec une motricité limitée ? Quels mouvements/clignotements éviter ? Quels réglages de volume et taille du texte ? |
| Enfants, publicité ou achats | Âges, contenu sensible, présence et emplacement des publicités, achats envisagés, pays de diffusion. Ces choix déclenchent une analyse spécifique avant intégration. [Public cible](https://support.google.com/googleplay/android-developer/answer/9867159?hl=en) · [Achats numériques](https://support.google.com/googleplay/android-developer/answer/10281818?hl=en) |

Proposition initiale pour le pilote à confirmer dans le brief : jeu solo, jouable hors ligne, sans compte, publicité ni achat. Les extensions restent des décisions explicites, avec leurs coûts et validations.

## Ce que GameForge doit déduire et vérifier

L'utilisateur décrit l'expérience. GameForge établit ensuite les paramètres, les ressources, les écrans et les tests nécessaires. Il affiche les hypothèses, contradictions et fonctionnalités non disponibles. Exemple de contradiction à résoudre : « tout fonctionne hors ligne » et « chat en temps réel ».

| Choix à demander | Vérification à effectuer par GameForge |
|---|---|
| Téléphones et orientation visés | Mesures sur appareil, mémoire, fluidité, reprise après interruption et adaptation de l'interface |
| Règles, difficulté et durée | Validité des situations, taux d'échec, progression praticable, parcours de recette |
| Ressources choisies | Licence, droit d'utilisation et de redistribution, attribution et budget matériel |
| Services, achats et données souhaités | Liste réelle des SDK et flux de données, permissions nécessaires, comportement réseau |
| Projet prêt à diffuser selon son créateur | AAB/signature, API et exigences courantes, tests, fiche boutique, déclarations et examen Play distincts |

Les seuils techniques évoluent et doivent être revalidés au moment de l'export. Pour la qualité du jeu, viser une session fluide et mesurer sur les appareils cibles plutôt que demander au créateur une valeur technique qu'il ne sait pas choisir. [Qualité technique Android](https://developer.android.com/quality/technical)

## Trois validations avant de coder

1. **Brief confirmé** — boucle jouable, commandes, objectif/échec, portée, ressources et hypothèses cohérents. Chaque fonction est indiquée « disponible », « adaptation nécessaire » ou « à développer ».
2. **Apparence retenue** — quatre propositions correspondant au même brief, régénérables. Le choix comprend les ressources réellement réalisables et une alternative si une génération externe manque.
3. **Gameplay retenu** — quatre façons de jouer compatibles avec cette apparence, règles et différences expliquées, régénérables. La confirmation lance la création de la scène.

Une demande hors capacité reste dans le brief et nécessite un plan de réalisation explicite avant de promettre un jeu complet. Le générateur actuel applique un sous-ensemble au Runner portrait à trois voies : palettes/thème, rythme/densité, objectifs et silhouettes low-poly. La génération locale suit des règles. OmniRoute peut enrichir les propositions, mais ne fournit actuellement ni générateur universel de code ni service de modèles 3D. Décrire un RPG ou du multijoueur ne rend pas ces systèmes disponibles.

## Préparer Google Play au bon moment

Le brief relève les choix qui changent le produit dès le départ. Le dossier de publication vient après le prototype testé. Les informations à préparer incluent public cible, classification du contenu, accès des examinateurs, publicité et confidentialité. La Play Console demande ces déclarations selon les fonctions présentes. [Préparation de l'examen](https://support.google.com/googleplay/android-developer/answer/9859455?hl=en)

La déclaration de sécurité des données doit correspondre aux flux réels, y compris les SDK. Même un jeu sans collecte doit généralement compléter le formulaire pour les pistes fermée, ouverte ou production ; Google précise une exemption pour les applications exclusivement en test interne. Le comportement du Studio PC, notamment sa dictée ou son fournisseur d'IA, est à distinguer des données traitées par le jeu exporté. [Sécurité des données](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en-EN)

Les réponses préparent un dossier à vérifier. Elles ne remplacent ni la recette sur appareil ni l'approbation du titulaire pour la diffusion, les dépenses et les déclarations finales. Les règles officielles devront être relues avant publication.

## Vérification de cette proposition

Analyse des 17 questions, du traitement `ForgeBriefDesign.cs` et de la fiche 024. Sources officielles consultées le 20 novembre 2026. Aucun schéma de sauvegarde ni comportement de génération modifié. À valider ensuite avec un scénario simple Runner, un puzzle et une demande complexe hors capacité, pour vérifier que les branches apparaissent au bon moment et que l'outil ne promet pas une fonction absente.
