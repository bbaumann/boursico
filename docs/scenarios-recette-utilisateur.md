# Scénarios de recette utilisateur

Ce document liste les scénarios à jouer pendant la recette utilisateur avant la première utilisation réelle de l'outil.

Il a servi à la première session de recette en juin 2026 et a été mis à jour à partir du code actuel et de l'historique Git. Il décrit la version la plus récente du code. La version jouée le 17 septembre 2026, sans le changement des références articles du 12 septembre, est dans `scenarios-recette-utilisateur-2026-09-17.md`. Les libellés entre accents graves sont ceux affichés à l'écran.

## Nouveautés depuis la recette de juin 2026

Les points ci-dessous méritent une attention particulière pendant cette session, car ils n'existaient pas ou fonctionnaient différemment en juin.

| Date | Changement | Scénarios concernés |
| --- | --- | --- |
| 05/06/2026 | Les champs obligatoires des formulaires portent un badge `Obligatoire` ; la ville est proposée dans une liste pour les codes postaux de l'Isère (vendeurs et caisse). | Prevente-03, Vente-02 |
| 05/06/2026 | L'import démarre dès que le fichier est choisi ; les lignes avec un titre mais sans prix valide sont importées avec un prix `À compléter`. | Prevente-01, Prevente-05 |
| 08/06/2026 | `Finaliser le dépôt` ouvre une fenêtre en trois étapes `Frais`, `Contrat`, `Signature` ; les blocages sont listés avant l'ouverture ; le contrat s'imprime dans un nouvel onglet. | Prevente-05 |
| 13/06/2026 | L'adresse de facturation peut être ajoutée après coup sur une vente, puis la facture imprimée. | Vente-06 |
| 13/06/2026 | Le nom de la banque propose une liste de banques courantes pour les chèques. | Vente-02 |
| 14/06/2026 | Les caisses physiques sont configurées dans `ADMIN` avec un nom et un fond de caisse ; la caisse se choisit dans une liste ; la clôture affiche le fond et les espèces attendues. | Prevente-09, Vente-01, Vente-10, PostVente-07 |
| 14/06/2026 | La feuille vendeur déduit des `Frais de vente (15%)` du montant à payer au vendeur. | PostVente-02, PostVente-06 |
| 14/06/2026 | La caisse affiche trois étapes `Acheteur`, `Articles`, `Paiement` ; les boutons finaux s'appellent `Valider sans imprimer` et `Valider et imprimer la facture`. | Vente-02, Vente-05 |
| 14/06/2026 | L'annulation d'une validation de dépôt est isolée dans une section `Administration` de la fiche vendeur. | Prevente-07 |
| 16/06/2026 | Un article déjà dans le panier est grisé avec la mention `Déjà dans le panier` au lieu d'afficher une erreur. | Vente-02 |
| 09/07/2026 | Les frais vendeur (attendus, payés, restants) sont vérifiés dans l'étape `Frais` de la finalisation du dépôt. | Prevente-05 |
| 29/08/2026 | L'application est livrée sous forme d'archive autonome avec un guide d'installation (`deploy/INSTALLATION.md`). | Installation-01 |
| 12/09/2026 | Les références articles changent de forme : une lettre et trois chiffres (`B157`), attribuées à l'étape `Frais` de la finalisation du dépôt et non plus à la saisie. La référence ne contient plus le code vendeur. | Prevente-04, Prevente-05, Prevente-07, Vente-02, PostVente-03 |
| 14/09/2026 | Correction : ajouter un lot d'articles juste après avoir ouvert puis fermé `Finaliser le dépôt` ne relançait pas la fenêtre, les articles n'étaient pas créés. | Prevente-04 |

## Classification

- `Bénévole` : scénario normal de l'événement. Il peut être long ou contenir un cas métier un peu complexe, mais il ne demande pas de mot de passe administrateur ni de correction exceptionnelle.
- `Avancé` : scénario réservé aux organisateurs qui connaissent le mot de passe administrateur, ou scénario qui modifie une situation sensible : statut de l'événement, import préparatoire, correction d'historique, remboursement, rapprochement exceptionnel.

## Installation

### Installation-01 - Démarrer l'application depuis l'archive livrée

- Niveau : `Avancé`
- Rôle typique : Admin

Description : vérifier que l'archive autonome démarre sur le poste serveur et que les autres postes y accèdent. À jouer seulement si la session utilise l'archive livrée plutôt qu'un poste de développement.

#### Étapes

1. Décompresser l'archive dans un dossier vide en suivant `deploy/INSTALLATION.md`.
2. Lancer `demarrer.bat` (Windows) ou `./demarrer.sh` (Debian).
3. Attendre la ligne `Application started` dans la fenêtre.
4. Ouvrir `http://localhost:5050` sur le serveur.
5. Ouvrir `http://<adresse-ip-du-serveur>:5050` depuis un second poste du réseau local.
6. Arrêter avec `Ctrl+C`, relancer, vérifier que les données sont toujours là.

#### Critères d'acceptation

- Aucun runtime .NET n'est à installer.
- Le premier démarrage crée le dossier `data/` et la base `bourse-aux-jouets.db`.
- Le statut de la vente et le nom de l'événement apparaissent en haut de chaque page.
- Les données survivent à un arrêt et un redémarrage.

## Avant la vente

### Prevente-01 - Importer les vendeurs depuis un formulaire internet

- Niveau : `Avancé`
- Rôle typique : Admin

Description : importer les soumissions CSV ou TSV produites par le formulaire internet externe.

#### Étapes

1. Ouvrir `VENDEURS`.
2. Cliquer sur `Importer des soumissions`.
3. Cliquer sur `Choisir un fichier CSV ou TSV` et sélectionner l'export. L'import démarre tout seul, il n'y a plus de bouton à cliquer ensuite.
4. Lire le `Résultat de l'import` : nombre de dossiers importés, de doublons possibles et de dossiers sans prénom ou nom.
5. Pour chaque ligne, vérifier les compteurs `articles prêts`, `articles à corriger` et `lignes non importées`.
6. Cliquer sur `Ouvrir` sur quelques vendeurs importés pour vérifier les articles et les notes.

#### Critères d'acceptation

- Les soumissions avec prénom et nom créent des vendeurs en `Dépôt à valider`.
- Les articles valides du champ `Jouet,Prix` sont ajoutés en `enregistré`.
- Une ligne avec un titre mais sans prix valide crée un article `enregistré` avec le prix `À compléter` et une note dans `Import à vérifier`.
- Une ligne sans titre récupérable n'est pas importée et apparaît seulement dans `Import à vérifier`.
- Les lignes invalides ne bloquent pas l'import du reste.
- Les coordonnées ou informations de pièce d'identité manquantes pourront être complétées plus tard.
- Une ville non reconnue laisse la ville vide, conserve le code postal et note la ville reçue dans `Import à vérifier`.

### Prevente-02 - Gérer un doublon détecté à l'import

- Niveau : `Avancé`
- Rôle typique : Admin

Description : traiter une soumission qui semble correspondre à un vendeur déjà présent.

#### Étapes

1. Importer un fichier contenant une soumission déjà connue.
2. Examiner la ligne marquée comme doublon possible : elle liste les vendeurs existants correspondants avec code, nom, téléphone et nombre d'articles.
3. Si un seul vendeur correspond, cliquer sur `Ajouter les articles au vendeur existant`, puis sur `Ouvrir le vendeur` pour contrôler.
4. Si plusieurs vendeurs correspondent, cliquer sur `Ignorer cette soumission` et traiter le cas manuellement.

#### Critères d'acceptation

- L'application ne fusionne pas automatiquement deux vendeurs.
- L'ajout au vendeur existant conserve les coordonnées déjà saisies.
- Le message `Soumission ajoutée au vendeur existant` indique le nombre d'articles ajoutés et le nombre d'articles déjà présents ignorés.
- Les articles déjà présents avec le même titre et le même prix ne sont pas ajoutés en double.

### Prevente-03 - Créer ou compléter un vendeur manuellement

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : créer un vendeur sans passer par l'import, ou compléter les informations d'un vendeur importé.

#### Étapes

1. Ouvrir `VENDEURS`.
2. Cliquer sur `Créer un vendeur`, ou ouvrir un vendeur existant puis cliquer sur `Modifier`.
3. Renseigner ou corriger les informations disponibles.
4. Saisir un code postal de l'Isère (`38xxx`) et vérifier que la ville est proposée dans une liste.
5. Saisir un code postal hors Isère et vérifier que la ville se tape librement.
6. Enregistrer.

#### Critères d'acceptation

- Le vendeur reçoit un code vendeur.
- Les champs `Prénom`, `Nom`, `Téléphone`, `Adresse postale`, `Code postal` et `Ville` portent le badge `Obligatoire`.
- Seuls le prénom et le nom bloquent l'enregistrement. Les autres champs marqués `Obligatoire` peuvent rester vides tant que le dépôt n'est pas validé.
- Les doublons évidents sont signalés avant enregistrement dans une liste `Vendeurs existants` avec un lien `Ouvrir ce vendeur`.
- Sans téléphone saisi, le doublon est détecté sur le prénom et le nom seuls.
- La liste `VENDEURS` se filtre par code ou nom et affiche code, nom, e-mail et nombre d'articles.

### Prevente-04 - Ajouter les articles d'un vendeur

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : ajouter les jouets prévus pour un vendeur depuis la fiche vendeur.

#### Étapes

1. Ouvrir la fiche vendeur.
2. Dans `Ajouter des articles`, saisir le titre et le prix vendeur de chaque article. Utiliser `+` pour ajouter des lignes.
3. Cliquer sur `Enregistrer le lot`.
4. Contrôle de régression : cliquer sur `Finaliser le dépôt`, cliquer sur `Retour` pour fermer la fenêtre, puis ajouter un nouveau lot avec `Enregistrer le lot`.

#### Critères d'acceptation

- Les articles sont créés en `enregistré`.
- Le prix vendeur doit être un montant entier en euros ; un prix avec centimes est refusé.
- Une ligne avec un titre sans prix, ou un prix sans titre, est refusée avec un message explicite.
- Les articles `enregistré` ne sont pas visibles en caisse.
- Les articles n'ont pas encore de référence ; l'écran affiche un tiret (`—`) à la place.
- Le lot ajouté après ouverture puis fermeture de `Finaliser le dépôt` est bien créé, sans rouvrir la fenêtre.
- Les prix saisis seront repris dans le contrat et dans les calculs vendeur.

### Prevente-05 - Finaliser le dépôt vendeur

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : confirmer les jouets physiquement déposés, attribuer les références, faire signer le contrat, puis rendre les articles vendables.

#### Étapes

1. Ouvrir la fiche vendeur.
2. Vérifier les articles apportés physiquement.
3. Supprimer ou corriger les articles créés par erreur avant validation.
4. Cliquer une première fois sur `Finaliser le dépôt` avec des informations manquantes pour voir la liste des blocages.
5. Compléter les informations obligatoires du vendeur via `Corriger la fiche vendeur`, y compris téléphone, adresse, code postal, ville et les quatre informations de pièce d'identité.
6. Cliquer sur `Finaliser le dépôt`. La fenêtre s'ouvre à l'étape `Frais`.
7. Vérifier les frais vendeur `Attendus`, `Payés` et `Reste`.
8. Renseigner `Frais payés` si le vendeur paie au dépôt, puis cliquer sur `Continuer`.
9. À l'étape `Contrat`, cliquer sur `Imprimer le contrat`. Le contrat s'ouvre dans un nouvel onglet et lance l'impression ; fermer l'onglet avec `Fermer cet onglet`.
10. Faire signer le contrat papier par le vendeur.
11. À l'étape `Signature`, cocher `Le contrat papier est signé par le vendeur.`, puis cliquer sur `Valider le dépôt`.

#### Critères d'acceptation

- Tant qu'il manque une information, `Finaliser le dépôt` n'ouvre pas la fenêtre et affiche un résumé `Fiche vendeur` et/ou `Articles` avec la liste des points à corriger.
- La validation est refusée tant qu'un article a un prix `À compléter`.
- La validation reste possible si des frais vendeur restent à payer ; l'étape `Frais` affiche alors `Déduit au rapprochement`.
- Avant `Continuer`, les articles n'ont pas de référence. Après `Continuer`, chaque article du vendeur a une référence de la forme une lettre plus trois chiffres, par exemple `B157`.
- Les articles d'un même vendeur finalisés ensemble portent la même lettre et des numéros qui se suivent.
- La lettre ne correspond pas au code vendeur : elle sert au tri physique des invendus après la vente.
- Le contrat imprimé contient l'identité de l'association, les informations vendeur, les frais vendeur et la liste des articles avec leur référence. Il n'imprime pas la pièce d'identité ni le statut de l'événement.
- `Valider le dépôt` reste grisé tant que la case de signature n'est pas cochée.
- Après validation, le message `Dépôt validé : N articles sont maintenant en stock.` s'affiche et les articles passent en `en stock`.
- Après validation, les modifications normales du vendeur et de ses articles sont verrouillées.
- Si `Continuer` affiche une erreur générale (deux postes finalisent au même instant), recliquer sur `Continuer` aboutit sans perte de données.

### Prevente-06 - Refuser un jouet avant signature

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : un jouet n'est pas apporté ou est détecté défectueux avant que le dépôt soit validé.

#### Étapes

1. Ouvrir la fiche vendeur avant validation du dépôt.
2. Identifier l'article absent ou refusé.
3. Cliquer sur `Supprimer` si la ligne a été créée par erreur ou si l'équipe veut nettoyer la fiche, puis confirmer.
4. Continuer la validation avec les articles acceptés.

#### Critères d'acceptation

- L'article refusé avant signature n'est pas mis en `défectueux`.
- L'article refusé n'apparaît pas en caisse.
- La validation du dépôt peut continuer avec les articles restants.
- Les frais vendeur attendus sont recalculés sur le nombre d'articles restants.

### Prevente-07 - Annuler une validation de dépôt faite par erreur

- Niveau : `Avancé`
- Rôle typique : Admin

Description : rouvrir exceptionnellement un dépôt validé pendant la phase `Prévente`.

#### Étapes

1. Vérifier que l'événement est en `Prévente`.
2. Ouvrir la fiche vendeur et descendre jusqu'à la section `Administration`.
3. Cliquer sur `Annuler la validation du dépôt`.
4. Saisir le mot de passe administrateur, puis cliquer sur `Annuler la validation`.
5. Corriger les informations ou les articles, par exemple ajouter un article oublié.
6. Rejouer `Finaliser le dépôt` jusqu'à `Valider le dépôt`.

#### Critères d'acceptation

- Seuls les articles encore `en stock` repassent en `enregistré`.
- Les articles déjà `vendu`, `manquant` ou `défectueux` restent inchangés et en lecture seule.
- Les articles repassés en `enregistré` gardent leur référence : les étiquettes déjà collées restent valables.
- Un article ajouté après réouverture reçoit, à `Continuer`, le numéro suivant dans la même lettre.
- Le dépôt doit être revalidé pour rendre les articles corrigés vendables.

### Prevente-08 - Changer le statut de l'événement

- Niveau : `Avancé`
- Rôle typique : Admin

Description : piloter les phases `Prévente`, `Vente` et `Après-vente`.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Cliquer successivement sur `Passer en prévente`, `Passer en vente`, puis `Passer en après-vente`.
4. Vérifier la destination de `/` après chaque changement.

#### Critères d'acceptation

- Le statut courant est affiché en haut de chaque page à côté du nom de l'événement.
- En `Prévente`, `/` ouvre le workflow vendeurs.
- En `Vente`, `/` ouvre la caisse et les fiches vendeurs passent en lecture seule.
- En `Après-vente`, `/` ouvre le rapprochement et la caisse affiche que le passage en caisse est fermé.
- Les ventes sont refusées côté serveur en `Après-vente`.
- Une fois déverrouillée, la page `ADMIN` ne redemande pas le mot de passe à chaque action.

### Prevente-09 - Configurer les caisses

- Niveau : `Avancé`
- Rôle typique : Admin

Description : déclarer les postes de caisse physiques et leur fond de caisse avant la vente.

#### Étapes

1. Ouvrir `ADMIN` et déverrouiller.
2. Dans `Caisses`, saisir un `Nom de la caisse` et un `Fond de caisse (€)`, laisser `Active` cochée, puis cliquer sur `Ajouter la caisse`.
3. Créer une seconde caisse.
4. Modifier le fond de caisse d'une caisse existante et cliquer sur `Enregistrer`.
5. Décocher `Active` sur une caisse et enregistrer.
6. Après avoir enregistré au moins une vente sur une caisse (voir Vente-02), tenter de la renommer.

#### Critères d'acceptation

- Deux caisses ne peuvent pas porter le même nom.
- Le fond de caisse ne peut pas être négatif.
- Une caisse désactivée disparaît de la liste proposée en `CAISSE` mais reste visible dans `ADMIN` et dans la clôture.
- Le nom d'une caisse déjà utilisée par une vente ou un remboursement est verrouillé, avec le message `Nom verrouillé car la caisse est déjà utilisée.`
- Le fond de caisse est repris dans la colonne `Fond` de l'onglet `Clôture`.

## Pendant la vente

### Vente-01 - Choisir la caisse du poste

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : identifier le poste de caisse physique avant d'encaisser.

#### Étapes

1. Ouvrir `CAISSE`.
2. Choisir une caisse active dans la liste `Caisse`.
3. Cliquer sur `Utiliser cette caisse`.
4. Recharger la page pour vérifier que le poste garde cette caisse.

#### Critères d'acceptation

- Tant qu'aucune caisse n'est choisie, les étapes de vente sont masquées et le message `Choisissez une caisse avant d'enregistrer une vente.` s'affiche.
- Après le choix, le message `Caisse active : <nom>` s'affiche.
- Le choix est conservé sur le poste après rechargement.
- Si la caisse choisie a été désactivée entre-temps, le poste redemande une caisse.
- Les ventes enregistrées comptent dans la ligne de cette caisse dans `DONNÉES` > `Clôture`.

### Vente-02 - Encaisser un client

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : vendre un ou plusieurs articles à un acheteur.

#### Étapes

1. Ouvrir `CAISSE`.
2. `Étape 1 - Acheteur` : renseigner `Nom ou structure`, `Code postal` et `Ville`. Pour un code postal de l'Isère, choisir la ville dans la liste proposée.
3. `Étape 2 - Articles` : chercher un article par référence (par exemple `B15`) ou par titre.
4. Cliquer sur `Ajouter` sur la bonne ligne.
5. Chercher à nouveau le même article : la ligne est grisée avec `Déjà dans le panier`.
6. Vérifier `Sous-total`, `Frais association familiale` et `Total`.
7. Cliquer sur `Finaliser`.
8. `Étape 3 - Paiement` : choisir `CB`, `Espèces` ou `Chèque`.
9. Pour un chèque, saisir le `Numéro de chèque` et le `Nom de la banque` ; la banque est proposée dans une liste de banques courantes mais reste modifiable.
10. Cliquer sur `Valider sans imprimer`.

#### Critères d'acceptation

- Seuls les articles `en stock` de vendeurs dont le dépôt est validé apparaissent dans la recherche.
- La recherche filtre à chaque frappe sur la référence et le titre.
- Un article déjà dans le panier ne peut pas être ajouté une deuxième fois.
- Les frais association familiale valent 10 % du sous-total, arrondis au centime.
- Le paiement par chèque est refusé sans numéro de chèque et nom de banque.
- Le message `Vente enregistrée.` s'affiche puis la caisse revient à un état vide pour le client suivant.
- Les articles vendus passent en `vendu` et disparaissent de la recherche.
- Les informations acheteur et paiement sont visibles dans `LISTE DES VENTES`.

### Vente-03 - Corriger un panier avant enregistrement

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : le bénévole a ajouté le mauvais article au panier, mais la vente n'est pas encore enregistrée.

#### Étapes

1. Ajouter volontairement un mauvais article au panier.
2. Cliquer sur `Retirer` sur cette ligne du panier.
3. Ajouter le bon article.
4. Finaliser normalement.

#### Critères d'acceptation

- La correction ne demande pas de mot de passe administrateur.
- Aucun historique de correction n'est créé tant que la vente n'est pas enregistrée.
- Seul le bon article est vendu.

### Vente-04 - Résoudre un conflit de disponibilité à la caisse

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : deux caisses ou deux opérateurs tentent de vendre le même article.

#### Étapes

1. Sur deux postes, ajouter le même article au panier.
2. Enregistrer la vente sur le premier poste.
3. Sur le second poste, cliquer sur `Finaliser`.
4. Lire le message d'erreur.
5. Retirer l'article en conflit.
6. Relancer la finalisation.

#### Critères d'acceptation

- La vente est refusée clairement, avant l'ouverture de la fenêtre de paiement.
- La ligne en conflit est mise en évidence en rouge dans le panier.
- Le reste du panier reste utilisable.
- Le bénévole peut terminer sans intervention administrateur.

### Vente-05 - Imprimer une facture pendant l'encaissement

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : le client demande une facture au moment de la vente.

#### Étapes

1. Remplir les informations acheteur obligatoires.
2. Remplir le champ `Adresse`.
3. Ajouter les articles au panier.
4. Cliquer sur `Finaliser`.
5. Cliquer sur `Valider et imprimer la facture`.

#### Critères d'acceptation

- `Valider et imprimer la facture` est grisé tant que l'adresse est vide.
- La vente est enregistrée avant l'impression.
- La facture porte un numéro unique de la forme `2611AFBJ0001`.
- La facture affiche l'association, le client, les lignes d'articles, les totaux HT et TTC et la mention de TVA.

### Vente-06 - Imprimer une facture après coup

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : le client demande une facture après l'enregistrement de la vente, alors qu'aucune adresse n'avait été saisie.

#### Étapes

1. Ouvrir `CAISSE`.
2. Cliquer sur `LISTE DES VENTES`.
3. Filtrer ou retrouver la vente, puis ouvrir le détail.
4. Dans `Adresse de facturation`, saisir l'adresse et cliquer sur `Enregistrer l'adresse`.
5. Cliquer sur `Imprimer la facture`.
6. Revenir sur la vente et cliquer une seconde fois sur `Imprimer la facture`.

#### Critères d'acceptation

- Sans adresse, `Imprimer la facture` n'est pas proposé et le message `Ajoutez une adresse de facturation avant d'imprimer la facture.` s'affiche.
- Une adresse vide est refusée à l'enregistrement.
- Après enregistrement de l'adresse, `Imprimer la facture` apparaît.
- La première impression crée le numéro de facture et la date d'émission, visibles dans la section `Facture`.
- La seconde impression réutilise le même numéro.

### Vente-07 - Consulter l'historique des ventes

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : retrouver une vente déjà enregistrée.

#### Étapes

1. Ouvrir `CAISSE`.
2. Cliquer sur `LISTE DES VENTES`.
3. Rechercher par numéro de vente, acheteur, code postal, ville, adresse, numéro de facture, mode de paiement, numéro de chèque, banque, référence ou titre d'article.
4. Ouvrir le détail d'une vente.

#### Critères d'acceptation

- La liste est accessible sans mot de passe administrateur, du plus récent au plus ancien.
- La colonne `Facture` indique `Oui` ou `Non`.
- Le détail est en lecture seule, à l'exception de l'adresse de facturation.
- Les lignes corrigées ou remboursées restent visibles comme historique, avec la date.

### Vente-08 - Corriger une vente enregistrée avec le mauvais article

- Niveau : `Avancé`
- Rôle typique : Admin

Description : une vente a été enregistrée avec une mauvaise référence et l'article doit pouvoir être revendu.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Dans `Correction des articles`, filtrer par référence, titre, vendeur, statut ou numéro de vente.
4. Trouver la ligne vendue par erreur.
5. Cliquer sur `Remettre en stock`.
6. Confirmer.

#### Critères d'acceptation

- L'article revient en `en stock`.
- La ligne de vente d'origine reste visible et marquée `Corrigée le <date>`.
- Les totaux historiques de la vente ne sont pas recalculés.
- La ligne corrigée ne compte plus dans le paiement vendeur.
- La référence n'indique plus le vendeur : la colonne `Vendeur` sert à identifier le vendeur.

### Vente-09 - Marquer un article accepté comme défectueux

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un article déjà accepté en stock est trouvé défectueux avant d'être vendu.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Filtrer `Correction des articles`.
4. Trouver l'article `en stock`.
5. Cliquer sur `Marquer défectueux` et confirmer.

#### Critères d'acceptation

- L'article passe en `défectueux`.
- Il disparaît de la caisse.
- Il apparaît dans les articles à rendre au vendeur.
- Il ne compte pas dans le paiement vendeur.
- Tant qu'aucun remboursement n'existe, `Remettre en stock` permet d'annuler le marquage.

### Vente-10 - Rembourser un article vendu puis rapporté défectueux

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un acheteur rapporte un article vendu parce qu'il est défectueux.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Trouver la ligne vendue.
4. Cliquer sur `Rembourser`.
5. Vérifier `Prix article`, `Frais association remboursés` et `Montant à rembourser` dans la fenêtre.
6. Choisir la `Caisse de remboursement` qui rend physiquement l'argent.
7. Cliquer sur `Confirmer le remboursement`.

#### Critères d'acceptation

- Le remboursement inclut le prix de l'article et les 10 % de frais association.
- Le remboursement est refusé sans caisse choisie.
- La vente d'origine reste visible avec son total d'origine, puis `Remboursements` et `Total corrigé`.
- La ligne est marquée `Remboursé` avec la date.
- L'article passe en `défectueux` et affiche `Remboursé - à rendre` dans `ADMIN`.
- L'article sera rendu au vendeur et ne sera pas payé au vendeur.
- Le remboursement apparaît dans la colonne `Remboursé` de la caisse choisie dans `Clôture`.

### Vente-11 - Annuler un remboursement défectueux saisi par erreur

- Niveau : `Avancé`
- Rôle typique : Admin

Description : le remboursement a été saisi par erreur et doit être annulé.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Retrouver la ligne remboursée.
4. Cliquer sur `Annuler le remboursement`.
5. Confirmer.

#### Critères d'acceptation

- Les champs de remboursement sont effacés.
- L'article redevient `vendu`.
- La vente peut de nouveau compter comme payable au vendeur.
- Le remboursement disparaît de la clôture.

## Après la vente

### PostVente-01 - Ouvrir le rapprochement

- Niveau : `Avancé`
- Rôle typique : Admin

Description : lancer les opérations de fin de vente.

#### Étapes

1. Ouvrir `RAPPROCHEMENT` alors que l'événement est encore en `Vente` et saisir le mot de passe administrateur.
2. Ouvrir `ADMIN` et cliquer sur `Passer en après-vente`.
3. Revenir dans `RAPPROCHEMENT`.

#### Critères d'acceptation

- Avant `Après-vente`, le rapprochement affiche `Le rapprochement est en lecture seule tant que la vente n'est pas en après-vente.` et ne propose ni impression ni action.
- En `Après-vente`, les nouvelles ventes sont refusées et la caisse annonce que le passage en caisse est fermé.
- En `Après-vente`, `Imprimer toutes les feuilles`, `Imprimer une feuille` et les actions de correction deviennent disponibles.

### PostVente-02 - Imprimer les fiches vendeurs

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : produire les feuilles utilisées pour rassembler les articles et préparer les paiements vendeurs.

#### Étapes

1. Ouvrir `RAPPROCHEMENT`.
2. Cliquer sur `Imprimer toutes les feuilles`.
3. Revenir, saisir un vendeur dans `Code ou nom du vendeur` (la liste propose `code - nom`), puis cliquer sur `Imprimer une feuille`.

#### Critères d'acceptation

- Les feuilles sont produites depuis `/rapprochement/feuilles`, une page par vendeur.
- Une feuille est générée pour chaque vendeur avec au moins un article `vendu`, `manquant`, `en stock` ou `défectueux`.
- Les `Articles à rendre` apparaissent avant les `Articles à payer`, avec une case `Trouvé` par ligne.
- Les articles `défectueux` sont marqués `DÉFECTUEUX`.
- Les articles `manquant` apparaissent dans les articles à payer avec le marqueur `MANQUANT`.
- Les totaux affichent `Sous-total articles`, `Frais de vente (15%)`, `Frais attendus`, `Frais payés`, `Frais restant dus` et `Total à payer`.

### PostVente-03 - Rassembler les articles à rendre

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : utiliser la fiche vendeur pour préparer les articles physiques à restituer.

#### Étapes

1. Trier les invendus physiques par lettre de référence, puis par numéro : chaque lettre forme une pile.
2. Prendre la fiche vendeur imprimée.
3. Rechercher les articles de la section `Articles à rendre` dans les piles correspondantes.
4. Cocher `Trouvé` sur les articles retrouvés.
5. Mettre les articles avec la fiche vendeur.

#### Critères d'acceptation

- La fiche est lisible sur papier.
- Les références d'un même vendeur sont groupées sous une même lettre avec des numéros qui se suivent, ce qui accélère la recherche.
- Les cases permettent de suivre le ramassage physique.
- Les articles défectueux sont rendus mais ne sont pas payés au vendeur.

### PostVente-04 - Marquer un article comme manquant

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un article encore en stock ne peut pas être retrouvé pendant le rapprochement.

#### Étapes

1. Ouvrir `RAPPROCHEMENT`.
2. Dans `Correction des articles`, filtrer par code vendeur, nom, référence ou titre.
3. Sur l'article `en stock`, cliquer sur `Marquer manquant`.
4. Cliquer sur le lien `Imprimer la feuille du vendeur <code>` proposé sous le message de confirmation.

#### Critères d'acceptation

- L'article passe en `manquant`.
- La fiche vendeur régénérée le déplace vers les articles à payer.
- L'article porte un marqueur `MANQUANT`.
- Le `Total à payer` inclut le prix de l'article manquant.

### PostVente-05 - Remettre en stock un article marqué manquant par erreur

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un article marqué `manquant` est finalement retrouvé avant finalisation du dossier vendeur.

#### Étapes

1. Ouvrir `RAPPROCHEMENT`.
2. Retrouver l'article `manquant`.
3. Cliquer sur `Remettre en stock`.
4. Réimprimer la fiche vendeur.

#### Critères d'acceptation

- L'article revient en `en stock`.
- Il repasse dans les articles à rendre.
- Le montant à payer au vendeur est recalculé.

### PostVente-06 - Préparer le dossier vendeur

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : finaliser le paquet physique remis au vendeur.

#### Étapes

1. Vérifier que la fiche vendeur finale est correcte.
2. Recalculer à la main le `Total à payer` sur un vendeur simple.
3. Préparer le chèque ou paiement selon le processus de l'association.
4. Regrouper la fiche finale, le paiement et les articles à rendre.
5. Préparer le retrait par le vendeur.

#### Critères d'acceptation

- `Total à payer` = `Sous-total articles` − `Frais de vente (15%)` − `Frais restant dus`, sans descendre sous zéro.
- Le sous-total compte les articles vendus non corrigés ni remboursés et les articles manquants.
- Les frais attendus valent 2 EUR par tranche commencée de 15 articles du vendeur, y compris les articles restés `enregistré`.
- Les articles défectueux sont rendus mais exclus du paiement vendeur.
- Le dossier vendeur est prêt pour le retrait.

### PostVente-07 - Faire le bilan des caisses

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : lire le bilan financier système avant comparaison avec la réalité physique des caisses.

#### Étapes

1. Ouvrir `DONNÉES`.
2. Aller dans l'onglet `Clôture`.
3. Vérifier la ligne `Toutes les caisses`.
4. Vérifier les lignes par caisse.
5. Comparer les colonnes `CB`, `Fond`, `Espèces`, `Espèces attendues`, `Chèques`, `Encaissé`, `Remboursé` et `Net` avec le contenu physique des caisses.

#### Critères d'acceptation

- La page affiche une ligne `Toutes les caisses` puis une ligne par caisse configurée.
- `Espèces attendues` = `Fond` + `Espèces` − `Remboursé` ; tous les remboursements sont considérés rendus en espèces par la caisse choisie.
- Les remboursements sont déduits de la caisse qui a physiquement remboursé, pas de celle qui a vendu.
- `Net` = `Encaissé` − `Remboursé`.

### PostVente-08 - Vérifier les chèques

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : contrôler les ventes payées par chèque.

#### Étapes

1. Ouvrir `DONNÉES`.
2. Aller dans l'onglet `Chèques`.
3. Filtrer par vente, numéro, banque ou montant.
4. Comparer les numéros, banques et montants avec les chèques physiques.
5. Ouvrir les ventes liées si un contrôle détaillé est nécessaire.

#### Critères d'acceptation

- Seules les ventes payées par chèque apparaissent.
- Chaque ligne affiche le numéro de vente, le numéro de chèque, la banque et le montant.
- Le numéro de vente permet d'ouvrir le détail en lecture seule.

## Scénarios à présenter en priorité

Pour une session de recette courte, jouer au minimum :

1. `Prevente-01` : import formulaire avec un cas simple et une ligne sans prix.
2. `Prevente-02` : traitement d'un doublon.
3. `Prevente-09` : configuration de deux caisses.
4. `Prevente-04` puis `Prevente-05` : saisie d'articles, puis finalisation d'un dépôt vendeur avec attribution des références et impression du contrat.
5. `Vente-01` puis `Vente-02` : choix de la caisse, encaissement simple avec paiement non chèque.
6. `Vente-02` : encaissement chèque avec numéro et banque.
7. `Vente-05` puis `Vente-06` : facture au moment de l'encaissement, puis ajout d'adresse et facture depuis l'historique.
8. `Vente-08` : correction avancée d'une mauvaise vente.
9. `Vente-09` puis `Vente-10` : article défectueux non vendu puis article vendu remboursé comme défectueux.
10. `PostVente-01`, `PostVente-02`, `PostVente-04` puis `PostVente-05` : passage en `Après-vente`, impression des fiches vendeurs, marquage `MANQUANT`, puis remise en stock.
11. `PostVente-06` : préparation du dossier vendeur avec contrôle du calcul à 15 %.
12. `PostVente-07` puis `PostVente-08` : bilan des caisses avec fond de caisse et contrôle des chèques.
