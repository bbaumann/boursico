# Use Cases And Personas

## Personas

### Sale-Day Volunteer

Profile:

- non-technical
- often elderly
- helping only during the event
- needs a crystal-clear interface and minimal training

Primary goal:

- enter item references and save customer sales reliably

What this persona does not need:

- seller information
- accounting concepts
- inventory administration
- payment method management

### Intake Operator

Profile:

- more comfortable with computer work
- prepares the event data before the sale

Primary goal:

- create sellers and items from seller-provided lists and confirm which preregistered toys were actually delivered

### Reconciliation Admin

Profile:

- more technical or more detail-oriented than sale-day volunteers
- works after the sale is finished
- operational role rather than a named software account

Primary goals:

- move the event between sale phases
- correct a sale line when the wrong reference was sold
- generate seller sheets
- mark items as `missing` when discovered
- regenerate final sheets

Access model in V1:

- administration requires the shared admin password to unlock `/administration`
- phase changes, sale-line corrections, and exceptional item corrections are available after that page is unlocked
- `missing` changes require the shared admin password
- this is a lightweight misuse-prevention control, not a full user/role system

### Payout Operator

Profile:

- operational user after the event
- may not be technical

Primary goal:

- use the final seller sheet to write checks and prepare seller pickup bags

## Use Cases

The current use cases are aligned with the French user acceptance test scenarios.

Classification:

- `Bénévole` : scénario normal de l'événement. Il peut être long ou contenir un cas métier un peu complexe, mais il ne demande pas de mot de passe administrateur ni de correction exceptionnelle.
- `Avancé` : scénario réservé aux organisateurs qui connaissent le mot de passe administrateur, ou scénario qui modifie une situation sensible : statut de l'événement, import préparatoire, correction d'historique, remboursement, rapprochement exceptionnel.

### Prevente-01 - Importer les vendeurs depuis un formulaire internet

- Niveau : `Avancé`
- Rôle typique : Admin

Description : importer les soumissions CSV ou TSV produites par le formulaire internet externe.

#### Étapes

1. Ouvrir `VENDEURS`.
2. Cliquer sur `Importer des soumissions`.
3. Choisir un export CSV ou TSV.
4. Cliquer sur `Importer les nouvelles soumissions`.
5. Ouvrir quelques vendeurs importés pour vérifier les articles et les notes.

#### Critères d'acceptation

- Les soumissions avec prénom et nom créent des vendeurs en `Dépôt à valider`.
- Les articles valides du champ `Jouet,Prix` sont ajoutés en `enregistré`.
- Les lignes invalides ne bloquent pas l'import et apparaissent dans `Import à vérifier`.
- Les coordonnées ou informations de pièce d'identité manquantes pourront être complétées plus tard.

### Prevente-02 - Gérer un doublon détecté à l'import

- Niveau : `Avancé`
- Rôle typique : Admin

Description : traiter une soumission qui semble correspondre à un vendeur déjà présent.

#### Étapes

1. Importer un fichier contenant une soumission déjà connue.
2. Examiner le résultat de doublon.
3. Si un seul vendeur correspond, choisir entre ouvrir le vendeur ou ajouter les articles au vendeur existant.
4. Si plusieurs vendeurs correspondent, ignorer la soumission et traiter le cas manuellement.

#### Critères d'acceptation

- L'application ne fusionne pas automatiquement deux vendeurs.
- L'ajout au vendeur existant conserve les coordonnées déjà saisies.
- Les articles déjà présents avec le même titre et le même prix ne sont pas ajoutés en double.

### Prevente-03 - Créer ou compléter un vendeur manuellement

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : créer un vendeur sans passer par l'import, ou compléter les informations d'un vendeur importé.

#### Étapes

1. Ouvrir `VENDEURS`.
2. Cliquer sur `Créer un vendeur` ou ouvrir un vendeur existant.
3. Renseigner ou corriger les informations disponibles.
4. Enregistrer.

#### Critères d'acceptation

- Le vendeur reçoit un code vendeur.
- Les doublons évidents sont signalés avant enregistrement.
- Les champs incomplets restent acceptés tant que le dépôt n'est pas validé.

### Prevente-04 - Ajouter les articles d'un vendeur

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : ajouter les jouets prévus pour un vendeur depuis la fiche vendeur.

#### Étapes

1. Ouvrir la fiche vendeur.
2. Ajouter les articles avec leur titre et leur prix.
3. Enregistrer les articles.

#### Critères d'acceptation

- Les articles sont créés en `enregistré`.
- Les articles `enregistré` ne sont pas visibles en caisse.
- Les articles n'ont pas encore de référence ; l'écran affiche un tiret (`—`) à la place.
- Les prix saisis seront repris dans le contrat et dans les calculs vendeur.

### Prevente-05 - Finaliser le dépôt vendeur

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : confirmer les jouets physiquement déposés, faire signer le contrat, puis rendre les articles vendables.

#### Étapes

1. Ouvrir la fiche vendeur.
2. Vérifier les articles apportés physiquement.
3. Supprimer ou corriger les articles créés par erreur avant validation.
4. Compléter les informations obligatoires du vendeur, y compris les informations de pièce d'identité.
5. Cliquer sur `Finaliser le dépôt`.
6. Vérifier les frais vendeur attendus, déjà payés et restant à payer.
7. Renseigner les frais vendeur payés au dépôt si nécessaire, puis cliquer sur `Continuer`.
8. Cliquer sur `Imprimer le contrat`.
9. Faire signer le contrat papier par le vendeur.
10. Cliquer sur `Valider le dépôt`, puis confirmer que le contrat est signé.

#### Critères d'acceptation

- La validation est refusée si les informations obligatoires sont manquantes.
- La validation reste possible si des frais vendeur restent à payer.
- Les articles n'ont pas de référence après la simple saisie des articles ; `Continuer` leur attribue une référence.
- Le contrat imprimé contient l'identité de l'association, les informations vendeur, les frais vendeur et la liste des articles avec leur référence.
- Après validation, les articles acceptés passent en `en stock`.
- Après validation, les modifications normales du vendeur et de ses articles sont verrouillées.

### Prevente-06 - Refuser un jouet avant signature

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : un jouet n'est pas apporté ou est détecté défectueux avant que le dépôt soit validé.

#### Étapes

1. Ouvrir la fiche vendeur avant validation du dépôt.
2. Identifier l'article absent ou refusé.
3. Supprimer la ligne si elle a été créée par erreur ou si l'équipe veut nettoyer la fiche.
4. Continuer la validation avec les articles acceptés.

#### Critères d'acceptation

- L'article refusé avant signature n'est pas mis en `défectueux`.
- L'article refusé n'apparaît pas en caisse.
- La validation du dépôt peut continuer avec les articles restants.

### Prevente-07 - Annuler une validation de dépôt faite par erreur

- Niveau : `Avancé`
- Rôle typique : Admin

Description : rouvrir exceptionnellement un dépôt validé pendant la phase `Prévente`.

#### Étapes

1. Vérifier que l'événement est en `Prévente`.
2. Ouvrir la fiche vendeur.
3. Cliquer sur `Annuler la validation du dépôt`.
4. Saisir le mot de passe administrateur.
5. Corriger les informations ou les articles.
6. Valider à nouveau le dépôt.

#### Critères d'acceptation

- Seuls les articles encore `en stock` repassent en `enregistré`.
- Les articles déjà `vendu`, `manquant` ou `défectueux` restent inchangés et en lecture seule.
- Le dépôt doit être revalidé pour rendre les articles corrigés vendables.

### Prevente-08 - Changer le statut de l'événement

- Niveau : `Avancé`
- Rôle typique : Admin

Description : piloter les phases `Prévente`, `Vente` et `Après-vente`.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Passer successivement en `Prévente`, `Vente`, puis `Après-vente`.
4. Vérifier la destination de `/` après chaque changement.

#### Critères d'acceptation

- En `Prévente`, `/` ouvre le workflow vendeurs.
- En `Vente`, `/` ouvre la caisse.
- En `Après-vente`, `/` ouvre le rapprochement.
- Les ventes sont refusées côté serveur en `Après-vente`.

### Vente-01 - Renseigner le nom de la caisse

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : choisir le poste de caisse physique configuré par l'administrateur avant d'encaisser.

#### Étapes

1. Ouvrir `CAISSE`.
2. Choisir une caisse active dans la liste.
3. Enregistrer ce choix.
4. Recharger la page pour vérifier que le poste garde cette caisse.

#### Critères d'acceptation

- La caisse ne permet pas d'encaisser tant qu'aucune caisse active n'est choisie.
- Le choix est conservé sur le poste.
- Les ventes enregistrées portent cette caisse.

### Vente-02 - Encaisser un client

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : vendre un ou plusieurs articles à un acheteur.

#### Étapes

1. Ouvrir `CAISSE`.
2. Renseigner `Nom ou structure`, `Code postal` et `Ville`.
3. Chercher un article par référence ou par titre.
4. Ajouter les articles au panier.
5. Vérifier le sous-total, les frais association et le total.
6. Cliquer sur `Finaliser`.
7. Choisir `CB`, `Espèces` ou `Chèque`.
8. Pour un chèque, saisir le numéro de chèque et le nom de la banque.
9. Cliquer sur `Enregistrer la vente`.

#### Critères d'acceptation

- Seuls les articles `en stock` de vendeurs validés apparaissent en caisse.
- Un article déjà dans le panier ne peut pas être ajouté une deuxième fois.
- La vente enregistrée passe les articles en `vendu`.
- La caisse revient à un état vide pour le client suivant.
- Les informations acheteur, paiement et caisse sont visibles dans l'historique.

### Vente-03 - Corriger un panier avant enregistrement

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : le bénévole a ajouté le mauvais article au panier, mais la vente n'est pas encore enregistrée.

#### Étapes

1. Ajouter volontairement un mauvais article au panier.
2. Le retirer du panier.
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

1. Garder un article dans le panier alors qu'il n'est plus disponible.
2. Cliquer sur `Finaliser` ou enregistrer la vente.
3. Lire le message d'erreur.
4. Retirer l'article en conflit.
5. Relancer la finalisation.

#### Critères d'acceptation

- La vente est refusée clairement.
- L'article en conflit est signalé.
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
5. Cliquer sur `Enregistrer et imprimer`.

#### Critères d'acceptation

- Le bouton d'impression est disponible seulement si l'adresse est renseignée.
- La vente est enregistrée avant l'impression.
- La facture porte un numéro unique.
- La facture affiche l'association, le client, les lignes d'articles, les totaux et la mention de TVA.

### Vente-06 - Imprimer une facture après coup

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : le client redemande une facture après l'enregistrement de la vente.

#### Étapes

1. Ouvrir `CAISSE`.
2. Cliquer sur `LISTE DES VENTES`.
3. Filtrer ou retrouver la vente.
4. Ouvrir le détail.
5. Cliquer sur `Imprimer la facture`.

#### Critères d'acceptation

- L'action est disponible seulement si l'adresse facture a été saisie sur la vente.
- Si un numéro existe déjà, il est réutilisé.
- Si aucun numéro n'existe encore, l'application en crée un une seule fois avant impression.

### Vente-07 - Consulter l'historique des ventes

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : retrouver une vente déjà enregistrée.

#### Étapes

1. Ouvrir `CAISSE`.
2. Cliquer sur `LISTE DES VENTES`.
3. Rechercher par acheteur, article, numéro de vente, facture, paiement ou chèque.
4. Ouvrir le détail d'une vente.

#### Critères d'acceptation

- La liste est accessible sans mot de passe administrateur.
- Le détail est en lecture seule.
- Les lignes corrigées ou remboursées restent visibles comme historique.

### Vente-08 - Corriger une vente enregistrée avec le mauvais article

- Niveau : `Avancé`
- Rôle typique : Admin

Description : une vente a été enregistrée avec une mauvaise référence et l'article doit pouvoir être revendu.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Filtrer `Correction des articles`.
4. Trouver la ligne vendue par erreur.
5. Cliquer sur `Remettre en stock`.
6. Confirmer.

#### Critères d'acceptation

- L'article revient en `en stock`.
- La ligne de vente d'origine reste visible et marquée comme corrigée.
- Les totaux historiques de la vente ne sont pas recalculés.
- La ligne corrigée ne compte plus dans le paiement vendeur.

### Vente-09 - Marquer un article accepté comme défectueux

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un article déjà accepté en stock est trouvé défectueux avant d'être vendu.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Filtrer `Correction des articles`.
4. Trouver l'article `en stock`.
5. Cliquer sur `Marquer défectueux`.

#### Critères d'acceptation

- L'article passe en `défectueux`.
- Il disparaît de la caisse.
- Il apparaît dans les articles à rendre au vendeur.
- Il ne compte pas dans le paiement vendeur.

### Vente-10 - Rembourser un article vendu puis rapporté défectueux

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un acheteur rapporte un article vendu parce qu'il est défectueux.

#### Étapes

1. Ouvrir `ADMIN`.
2. Déverrouiller avec le mot de passe administrateur.
3. Trouver la ligne vendue.
4. Cliquer sur `Rembourser`.
5. Choisir la caisse qui effectue physiquement le remboursement.
6. Confirmer le remboursement.

#### Critères d'acceptation

- Le remboursement inclut le prix de l'article et les 10 % de frais association.
- La vente d'origine reste visible avec son total d'origine.
- La ligne est marquée `Remboursé`.
- L'article passe en `défectueux`.
- L'article sera rendu au vendeur et ne sera pas payé au vendeur.
- Le remboursement apparaît dans le bilan de clôture de la caisse choisie.

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

### PostVente-01 - Ouvrir le rapprochement

- Niveau : `Avancé`
- Rôle typique : Admin

Description : lancer les opérations de fin de vente.

#### Étapes

1. Ouvrir `ADMIN`.
2. Passer l'événement en `Après-vente`.
3. Ouvrir `RAPPROCHEMENT`.
4. Saisir le mot de passe administrateur.

#### Critères d'acceptation

- Les nouvelles ventes sont refusées.
- Les actions de rapprochement sont disponibles seulement en `Après-vente`.
- Le rapprochement peut être consulté avant `Après-vente`, mais reste en lecture seule.

### PostVente-02 - Imprimer les fiches vendeurs

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : produire les feuilles utilisées pour rassembler les articles et préparer les paiements vendeurs.

#### Étapes

1. Ouvrir `RAPPROCHEMENT`.
2. Cliquer sur `Imprimer toutes les feuilles`.
3. Tester aussi l'impression d'une seule feuille avec `Code ou nom du vendeur`.

#### Critères d'acceptation

- Les feuilles sont produites depuis `/rapprochement/feuilles`.
- Une feuille est générée pour chaque vendeur avec au moins un article `vendu`, `manquant`, `en stock` ou `défectueux`.
- Les articles à rendre apparaissent avant les articles à payer.
- Les articles `défectueux` sont marqués visiblement.
- Les articles `manquant` apparaissent dans les articles à payer avec un marqueur visible.

### PostVente-03 - Rassembler les articles à rendre

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : utiliser la fiche vendeur pour préparer les articles physiques à restituer.

#### Étapes

1. Prendre la fiche vendeur imprimée.
2. Rechercher les articles de la section `Articles à rendre`.
3. Cocher les articles retrouvés.
4. Mettre les articles avec la fiche vendeur.

#### Critères d'acceptation

- La fiche est lisible sur papier.
- Les cases permettent de suivre le ramassage physique.
- Les articles défectueux sont rendus mais ne sont pas payés au vendeur.

### PostVente-04 - Marquer un article comme manquant

- Niveau : `Avancé`
- Rôle typique : Admin

Description : un article encore en stock ne peut pas être retrouvé pendant le rapprochement.

#### Étapes

1. Ouvrir `RAPPROCHEMENT`.
2. Filtrer par code vendeur, nom, référence ou titre.
3. Sur l'article `en stock`, cliquer sur `Marquer manquant`.
4. Réimprimer la fiche vendeur.

#### Critères d'acceptation

- L'article passe en `manquant`.
- La fiche vendeur régénérée le déplace vers les articles à payer.
- L'article porte un marqueur `MANQUANT`.

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
2. Préparer le chèque ou paiement selon le processus de l'association.
3. Regrouper la fiche finale, le paiement et les articles à rendre.
4. Préparer le retrait par le vendeur.

#### Critères d'acceptation

- Le montant final tient compte des articles vendus, des articles manquants et des frais vendeur restants.
- Les articles défectueux sont rendus mais exclus du paiement vendeur.
- Le dossier vendeur est prêt pour le retrait.

### PostVente-07 - Faire le bilan des caisses

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : lire le bilan financier système avant comparaison avec la réalité physique des caisses.

#### Étapes

1. Ouvrir `DONNÉES`.
2. Aller dans l'onglet `Clôture`.
3. Vérifier la ligne globale.
4. Vérifier les lignes par caisse.
5. Comparer les colonnes `CB`, `Espèces`, `Chèques`, `Total encaissé`, `Remboursements` et `Total net`.

#### Critères d'acceptation

- La page affiche une ligne `Toutes les caisses`.
- Chaque caisse utilisée a sa propre ligne.
- Les remboursements sont déduits de la caisse qui a physiquement remboursé.
- Le total net correspond au total encaissé moins les remboursements.

### PostVente-08 - Vérifier les chèques

- Niveau : `Bénévole`
- Rôle typique : Bénévole

Description : contrôler les ventes payées par chèque.

#### Étapes

1. Ouvrir `DONNÉES`.
2. Aller dans l'onglet `Chèques`.
3. Comparer les numéros, banques et montants avec les chèques physiques.
4. Ouvrir les ventes liées si un contrôle détaillé est nécessaire.

#### Critères d'acceptation

- Seules les ventes payées par chèque apparaissent.
- Chaque ligne affiche le numéro de vente, le numéro de chèque, la banque et le montant.
- Le numéro de vente permet d'ouvrir le détail en lecture seule.

## UX Implications From Personas

- The sale-day flow must prioritize speed, readability, and lack of ambiguity.
- The system must avoid exposing unnecessary data during sales.
- Reconciliation can tolerate a more technical workflow because it is used later by different people.
