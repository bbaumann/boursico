# Guide utilisateur - Outil de vente de l'association

Ce guide explique l'outil du point de vue des organisateurs, des bénévoles et des personnes qui préparent le rapprochement après la vente. Il est volontairement plus simple que la documentation technique : il décrit les usages, les écrans importants et la logique de sécurité opérationnelle.

## Idée générale

L'outil sert à gérer une vente annuelle de jouets pour une association.

Il remplace les fichiers Excel et les consolidations manuelles de fin de journée par une seule base de données centrale. Chaque jouet physique correspond à un article dans l'application. Chaque vente est enregistrée immédiatement. Après la vente, les feuilles vendeurs sont générées à partir des faits enregistrés dans l'outil.

Le but n'est pas de créer un logiciel de caisse complet ni un ERP. Le but est de rendre l'événement plus sûr, plus simple et plus facile à vérifier.

## Philosophie de l'outil

### Simple pendant la vente

Le jour de la vente, les bénévoles doivent pouvoir encaisser avec très peu de formation. L'écran de caisse ne montre pas les informations vendeurs, limite le paiement à un choix simple au moment de finaliser, ne dépend pas d'un scanner, et ne demande pas de gérer des notions comptables.

Le bénévole cherche un article, l'ajoute au panier, vérifie le total, puis enregistre la vente.

### Une seule source de vérité

La base centrale est la référence numérique de l'événement. Elle contient les vendeurs, les articles, les ventes et les corrections.

Il n'y a pas de synchronisation hors ligne entre plusieurs postes. Si le serveur central n'est plus disponible, on arrête la vente numérique et on passe au papier.

### Des statuts d'événement pour éviter les erreurs

L'événement a toujours un statut visible en haut de l'écran :

- `Prévente` : préparation et corrections encore possibles, ventes possibles.
- `Vente` : ventes possibles, mais les informations vendeurs et articles sont verrouillées.
- `Après-vente` : ventes fermées, rapprochement disponible.

Ces statuts ne sont pas seulement informatifs. Le serveur applique les règles correspondantes. Par exemple, une vente ne peut pas être enregistrée en `Après-vente`, même si une page de caisse était déjà ouverte.

### Mot de passe pour les actions risquées

L'outil n'a pas de comptes utilisateurs individuels. Certaines actions sont simplement protégées par un mot de passe administrateur partagé :

- changer le statut de l'événement ;
- corriger une ligne de vente ;
- rembourser un article vendu puis rapporté défectueux ;
- ouvrir le rapprochement ;
- marquer un article comme manquant ;
- remettre en stock un article marqué manquant par erreur ;
- marquer ou corriger un article défectueux.

Ce mot de passe n'est pas une sécurité forte au sens informatique. Il sert surtout à éviter qu'un bénévole modifie par erreur une information sensible.

### Correction explicite, jamais de réécriture invisible

Quand une vente est enregistrée, elle reste dans l'historique. Si un mauvais article a été vendu par erreur, l'administrateur corrige la ligne de vente : l'article revient en stock, mais la vente d'origine reste visible dans la base.

Même logique après la vente : un article marqué `MANQUANT` peut être remis en stock si c'était une erreur, mais l'action passe par le workflow de rapprochement.

## Les rôles

### Opérateur de saisie

Il prépare les données avant la vente :

- crée les vendeurs ;
- saisit les articles ;
- confirme les articles réellement déposés ;
- corrige les informations tant que le statut le permet.

### Bénévole de caisse

Il utilise uniquement l'écran `CAISSE` :

- cherche les articles par référence ou par description ;
- ajoute les articles au panier ;
- finalise la vente ;
- imprime éventuellement une facture si le client la demande.

### Administrateur de vente

Il connaît le mot de passe administrateur. Il peut :

- changer le statut de l'événement ;
- corriger une ligne de vente ;
- ouvrir le rapprochement ;
- traiter les articles manquants après la vente.

### Opérateur de rapprochement

Il travaille après la vente :

- imprime les feuilles vendeurs ;
- rassemble les articles à rendre ;
- prépare les chèques ou paiements vendeurs ;
- regroupe la feuille finale, les articles retournés et le paiement.

## Navigation principale

L'application a quatre entrées principales :

- `VENDEURS` : création des vendeurs et saisie des articles.
- `CAISSE` : vente au client.
- `RAPPROCHEMENT` : feuilles vendeurs et corrections après-vente.
- `ADMIN` : statut de la vente et corrections de ventes.

La page `/` redirige automatiquement vers le bon workflow selon le statut :

- en `Prévente`, vers `/vendeurs` ;
- en `Vente`, vers `/caisse` ;
- en `Après-vente`, vers `/rapprochement`.

## Avant la vente : préparer les vendeurs et les articles

### Créer un vendeur

Aller dans `VENDEURS`, puis cliquer sur `Créer un vendeur`.

Renseigner au minimum le prénom et le nom. Ajouter les informations disponibles :

- prénom ;
- nom ;
- e-mail si disponible ;
- téléphone si disponible ;
- adresse postale, code postal et ville si disponibles ;
- nature, numéro et date de délivrance de la pièce d'identité si disponibles ;
- notes si nécessaire.

À l'enregistrement, l'outil attribue un code vendeur. Ce code sert à retrouver le vendeur dans les recherches et les listes ; il n'apparaît pas dans la référence de ses articles, qui n'est attribuée que plus tard, au moment du dépôt. Les coordonnées et les informations de pièce d'identité manquantes peuvent être complétées plus tard, mais elles sont obligatoires avant la validation du dépôt.

### Importer des soumissions de formulaire

Si les vendeurs ont rempli un formulaire en ligne externe, exporter les réponses au format CSV ou TSV, puis aller dans `VENDEURS` et cliquer sur `Importer des soumissions`.

Cliquer sur `Fichier CSV ou TSV`, choisir l'export, puis cliquer sur `Importer les nouvelles soumissions`.

L'import crée des fiches vendeurs en `Dépôt à valider` quand la soumission contient au moins un prénom et un nom. Les coordonnées et informations de pièce d'identité incomplètes ne bloquent pas l'import : elles seront complétées avec le vendeur avant la signature du contrat.

Les articles valides du champ `Jouet,Prix` sont ajoutés en `enregistré`. Quand une ligne contient une description mais pas de prix valide, l'article est aussi ajouté en `enregistré` avec un prix `À compléter`, et une note est ajoutée dans `Import à vérifier`. Quand la description ne peut pas être extraite, la ligne n'est pas importée et reste seulement notée dans `Import à vérifier`.

Si une ville reçue ne peut pas être reconnue, le code postal est conservé, la ville reste vide, et la ville reçue est notée dans `Import à vérifier`.

Si une soumission correspond à un vendeur existant, elle n'est pas importée automatiquement. Quand un seul vendeur existant correspond, cliquer sur `Ajouter les articles au vendeur existant` si les articles doivent être ajoutés à cette fiche. Les coordonnées du vendeur existant ne sont pas remplacées. Les articles déjà présents avec la même description et le même prix sont ignorés. Quand plusieurs vendeurs existants correspondent, cliquer sur `Ignorer cette soumission`.

### Éviter les doublons vendeurs

Quand on crée ou modifie un vendeur, l'outil bloque les doublons évidents.

Si un vendeur similaire existe déjà, la page affiche la liste des vendeurs existants avec :

- code vendeur ;
- nom ;
- téléphone ;
- e-mail ou indication `Sans e-mail` ;
- nombre d'articles ;
- lien pour ouvrir le vendeur existant.

Dans ce cas, il faut ouvrir le vendeur existant au lieu d'en créer un second, sauf si l'organisateur confirme qu'il s'agit bien d'une personne différente.

Si aucun téléphone n'est saisi, le doublon est bloqué sur le prénom et le nom seuls.

### Ajouter des articles

Ouvrir la fiche du vendeur, puis utiliser la section `Ajouter des articles`.

Pour chaque article, saisir :

- la description ;
- le prix vendeur en euros.

Le prix vendeur est aussi le prix imprimé vu par le client dans l'outil actuel. Il sert aussi au calcul du montant à reverser au vendeur si l'article est vendu ou marqué manquant.

À droite du bouton `+`, un compteur affiche le nombre total d'articles du vendeur, en comptant les lignes en cours de saisie, par exemple `14 articles (dont 3 nouveaux)`. Il se met à jour pendant la saisie et aide le vendeur à savoir combien d'articles il dépose avant d'enregistrer. Le même compteur apparaît lors de la création d'un vendeur.

### Valider le dépôt vendeur

Les articles ajoutés restent `enregistré` tant que le dépôt du vendeur n'est pas validé. Ils ne sont pas visibles en caisse. Tant qu'ils n'ont pas de référence, l'écran affiche un tiret (`—`) à la place.

Les planches d'étiquettes à préparer avant l'événement doivent utiliser exactement 20 lettres : `B C D E G H J K L M N P R S T U W X Y Z` (sans A, F, I, O, Q, V), chacune numérotée de `001` à `455`. Une planche imprimée avec A à Z, avec une plage `001`-`500`, ou avec une lettre en moins, ne correspondra pas aux références attribuées par l'outil, et l'écart n'apparaîtra qu'à l'accueil des dépôts le jour de la vente.

Quand le vendeur apporte les jouets :

1. Ouvrir sa fiche vendeur.
2. Relire les articles un par un.
3. Compléter le téléphone, l'adresse postale, le code postal, la ville et les informations de pièce d'identité du vendeur : nature de la pièce, numéro et date de délivrance.
4. Corriger ou supprimer les lignes incomplètes avant signature.
5. Cliquer sur `Finaliser le dépôt`.
6. Vérifier les frais vendeurs attendus, déjà payés et restant à payer. Corriger `Frais payés` si le vendeur règle tout ou partie des frais à ce moment.
7. Cliquer sur `Continuer`. À cette étape, l'outil attribue une référence à chaque article du vendeur qui n'en a pas encore. Si `Continuer` affiche une erreur générale (deux postes ont finalisé un dépôt au même instant), les frais du vendeur sont déjà enregistrés et aucune donnée n'est perdue : cliquer à nouveau sur `Continuer` suffit, la seconde tentative aboutit normalement.
8. Cliquer sur `Imprimer le contrat`. Le contrat imprimé liste les articles avec leur référence.
9. Faire signer le contrat papier par le vendeur.
10. Cliquer sur `Valider le dépôt`, puis confirmer `Le contrat est signé`.

Les frais restants ne bloquent pas la validation du dépôt. S'il reste un montant à payer, il sera déduit lors du rapprochement vendeur.

Le contrat imprimé affiche l'identité de l'association, son adresse, son SIRET, les frais vendeurs attendus, les frais déjà payés, les mentions légales, la liste des articles et les prix. Il n'affiche pas les informations de pièce d'identité du vendeur. Il ne contient qu'une signature vendeur, avec la phrase `J'accepte le règlement de la bourse aux jouets` juste avant la date de signature. Le statut de l'événement n'est pas imprimé sur le contrat.

La validation du dépôt passe les articles acceptés `en stock` et verrouille les modifications normales du vendeur et de ses articles. Elle est bloquée tant qu'un article a un prix `À compléter`. Le contrat papier signé est la preuve du dépôt et de l'accord sur les prix.

Si le dépôt doit être rouvert exceptionnellement, revenir en `Prévente`, ouvrir la fiche vendeur, cliquer sur `Annuler la validation du dépôt`, puis saisir le mot de passe administrateur. Les articles encore `en stock` repassent `enregistré` et redeviennent modifiables. Les articles déjà `vendu`, `manquant` ou `défectueux` restent inchangés et en lecture seule. Après correction, cliquer à nouveau sur `Valider le dépôt`.

Attention : après une telle réouverture, les articles redevenus `enregistré` gardent la référence déjà attribuée, et supprimer l'un d'eux ne libère pas forcément son étiquette. Supprimer l'article portant le numéro le plus élevé d'une lettre abaisse le compteur de cette lettre : le prochain vendeur pourra recevoir exactement la même référence, alors que l'étiquette déjà imprimée est peut-être collée sur un jouet retiré. Ne pas supposer qu'une suppression libère l'étiquette correspondante.

Si un article n'est pas apporté, le laisser `enregistré` si le vendeur peut revenir plus tard. Le supprimer seulement si l'entrée a été créée par erreur ou si l'équipe veut nettoyer la fiche. Si un défaut est vu avant la signature, ne pas accepter l'article et supprimer la ligne si elle existe.

Un article `enregistré` n'apparaît ni en caisse ni dans les feuilles vendeurs finales. Il reste toutefois compté dans les articles du vendeur pour les frais d'inscription.

## Pendant la vente : encaisser un client

Aller dans `CAISSE` ou ouvrir `/caisse`.

### Renseigner l'acheteur

Avant de finaliser une vente, remplir les champs `Nom ou structure`, `Code postal` et `Ville`.

Pour les codes postaux de l'Isère, la ville est proposée automatiquement et peut être modifiée si nécessaire. Pour les autres départements, saisir la ville librement.

### Chercher un article

Dans `Rechercher un article`, taper une partie de la référence ou de la description.

La table se filtre immédiatement. Seuls les articles `en stock` dont le dépôt vendeur est validé apparaissent. Le bénévole choisit explicitement la bonne ligne. Il n'y a pas besoin de connaître le vendeur.

### Ajouter au panier

Cliquer sur l'article dans la liste filtrée. L'article apparaît dans le panier avec son prix.

Le panier affiche automatiquement :

- sous-total ;
- frais association familiale ;
- total.

Les frais association familiale correspondent à 10 % du sous-total, arrondis au centime. Le montant payé par le client est donc :

`total = somme des prix articles + frais association familiale`.

Le vendeur, lui, est payé sur le prix vendeur des articles, pas sur les frais association.

### Finaliser la vente

Quand le client a terminé :

1. Cliquer sur `Finaliser`.
2. Vérifier le nombre d'articles et les montants.
3. Choisir le mode de paiement : `CB`, `Espèces` ou `Chèque`.
4. Pour un paiement par chèque, saisir le numéro du chèque et le nom de la banque.
5. Cliquer sur `Enregistrer la vente`.

L'application vérifie les articles avant d'afficher la fenêtre finale, puis revérifie au moment exact de l'enregistrement. Cela évite de vendre deux fois le même article si deux caisses travaillent en même temps.

Si un article n'est plus disponible, la vente est refusée clairement. Le bénévole doit retirer l'article en conflit du panier, puis recommencer la finalisation.

### Imprimer une facture

La facture est optionnelle.

Si le client demande une facture, remplir `Adresse` avec l'adresse client à imprimer.

Ensuite, dans la fenêtre de finalisation, le bouton `Enregistrer et imprimer` devient disponible. L'application enregistre d'abord la vente, crée le numéro de facture, puis ouvre l'impression du navigateur.

Si le champ d'adresse est vide, l'option d'impression reste désactivée.

La facture contient son numéro unique dans le titre, la date d'émission, le logo de l'Association Familiale, l'identité de l'Association Familiale, un bloc client avec le nom, l'adresse, le code postal et la ville, les lignes d'articles, les totaux HT/TTC et la mention `TVA non applicable en vertu de l'article 293 B du CGI.`

## Administration

Aller dans `ADMIN` ou `/administration`.

La page demande le `Mot de passe administrateur`. Une fois déverrouillée dans le navigateur, elle permet de changer le statut de la vente et de corriger des articles exceptionnels sans redemander le mot de passe à chaque clic.

### Prévente

Utiliser `Prévente` avant l'événement et pendant les dernières corrections.

Dans ce statut :

- les vendeurs sont modifiables ;
- les articles non vendus sont modifiables ;
- les ventes restent possibles ;
- `/` ouvre le workflow vendeurs.

Ce statut évite de bloquer l'événement si les organisateurs font encore quelques ajustements.

### Vente

Utiliser `Vente` quand on veut figer les données principales.

Dans ce statut :

- les ventes restent possibles ;
- les vendeurs et les articles ne sont plus modifiables en fonctionnement normal ;
- seuls les changements de workflow restent autorisés, par exemple `en stock` vers `vendu` quand une vente est enregistrée ;
- `/` ouvre la caisse.

### Après-vente

Utiliser `Après-vente` quand la vente est terminée.

Dans ce statut :

- la caisse est fermée ;
- les nouvelles ventes sont refusées côté serveur ;
- les données vendeurs et articles restent verrouillées ;
- les feuilles vendeurs et les corrections de rapprochement deviennent disponibles ;
- `/` ouvre le rapprochement.

## Corriger une vente enregistrée avec le mauvais article

Cas typique : un bénévole a tapé ou choisi une mauvaise référence, et l'article enregistré comme vendu doit pouvoir être revendu.

Aller dans `ADMIN`, déverrouiller la page, puis utiliser `Correction des articles`.

1. Filtrer les articles affichés par référence, description, vendeur, statut ou numéro de vente.
2. Trouver le mauvais article vendu.
3. Cliquer sur `Remettre en stock`.
4. Confirmer l'action.

Résultat :

- l'article revient `en stock` ;
- il peut être vendu à nouveau ;
- la ligne de vente d'origine reste marquée comme corrigée ;
- les totaux de la vente d'origine ne sont pas recalculés ;
- la ligne corrigée ne compte plus dans le paiement vendeur.

Cette règle évite de réécrire l'historique de caisse tout en corrigeant le stock et le futur rapprochement.

## Gérer un article défectueux

Si un défaut est trouvé après l'acceptation de l'article, aller dans `ADMIN`, déverrouiller la page, puis utiliser `Correction des articles`.

Pour un article encore `en stock` :

1. Filtrer les articles affichés par référence, description, vendeur ou statut.
2. Cliquer sur `Marquer défectueux`.

Résultat :

- l'article ne peut plus être vendu ;
- il apparaît dans les articles à rendre au vendeur ;
- il ne compte pas dans le paiement vendeur.

Si le marquage était une erreur et qu'aucun acheteur n'a été remboursé pour cet article, l'administrateur peut cliquer sur `Remettre en stock`.

Si un acheteur rapporte un article vendu parce qu'il est défectueux :

1. Aller dans `Correction des articles`.
2. Trouver l'article vendu.
3. Cliquer sur `Rembourser`.
4. Vérifier le montant affiché dans la fenêtre de confirmation.
5. Rembourser l'acheteur du total indiqué, puis confirmer.

Le montant inclut le prix de l'article et les 10 % de frais association. La vente d'origine reste visible avec son total d'origine, la ligne est marquée `Remboursé` avec la date du remboursement, puis les totaux affichent `Remboursements` et `Total corrigé`. L'article sera rendu au vendeur et ne sera pas payé au vendeur.

Si le remboursement a été saisi par erreur, l'administrateur peut cliquer sur `Annuler le remboursement`. L'article redevient `vendu` et la ligne de vente n'est plus marquée comme remboursée.

## Après la vente : rapprochement

Aller dans `RAPPROCHEMENT` ou `/rapprochement`.

La page demande le mot de passe administrateur. Avant le statut `Après-vente`, elle peut être consultée, mais les actions de rapprochement restent en lecture seule.

### Imprimer les feuilles vendeurs

En `Après-vente`, utiliser :

- `Imprimer toutes les feuilles` pour générer toutes les feuilles ;
- `Code ou nom du vendeur` puis `Imprimer une feuille` pour sortir une seule feuille.

Les feuilles sont disponibles sur `/rapprochement/feuilles`.

Une feuille vendeur contient :

- l'identité du vendeur ;
- les articles à payer ;
- les articles à rendre ;
- le total à payer au vendeur.

Les articles à payer sont :

- les articles vendus ;
- les articles marqués `MANQUANT`.

Les articles à rendre sont les articles encore `en stock` et les articles `DÉFECTUEUX`.

Les articles seulement `enregistré` sont exclus des feuilles, car ils n'ont jamais été confirmés comme présents.

### Rassembler les articles à rendre

Pour chaque vendeur :

1. Imprimer la feuille vendeur.
2. Prendre la liste `Articles à rendre`.
3. Chercher physiquement les articles.
4. Mettre les articles trouvés avec la feuille du vendeur.

Si tous les articles à rendre sont trouvés, la feuille peut servir de feuille finale.

### Marquer un article manquant

Si un article censé être rendu n'est pas retrouvé physiquement :

1. Revenir dans `RAPPROCHEMENT`.
2. Filtrer les articles par référence, description ou vendeur.
3. Sur l'article `en stock`, cliquer sur `Marquer manquant`.
4. Réimprimer la feuille du vendeur.

L'article passe dans les `Articles à payer` avec le marqueur `MANQUANT`. Il est alors inclus dans le total à payer au vendeur.

### Corriger un article marqué manquant par erreur

Si l'article est retrouvé avant la finalisation du dossier vendeur :

1. Dans `RAPPROCHEMENT`, filtrer l'article.
2. Sur l'article `manquant`, cliquer sur `Remettre en stock`.
3. Réimprimer la feuille vendeur.

L'article revient dans `Articles à rendre`.

### Préparer le dossier vendeur

Une fois la feuille correcte :

1. Vérifier le total à payer.
2. Préparer le chèque ou paiement selon le processus de l'association.
3. Regrouper la feuille finale, le paiement et les articles à rendre.
4. Préparer le retrait par le vendeur.

L'outil calcule les frais vendeurs attendus : `2 EUR` par tranche commencée de 15 articles saisis pour le vendeur, y compris ceux encore au statut `enregistré`. Les frais déjà payés sont saisis sur la fiche vendeur. S'il reste des frais dus, ils sont déduits du montant final à payer au vendeur.

## En cas de panne

Si le serveur central ou l'application n'est plus disponible pendant l'événement :

1. Noter l'heure de la panne.
2. Arrêter la vente numérique.
3. Passer aux factures papier pré-numérotées.
4. Garder les factures papier terminées ensemble et dans l'ordre.
5. Continuer l'événement manuellement.
6. Faire le rapprochement après l'événement à partir des factures papier et du stock physique restant.

L'outil ne tente pas de rejouer plus tard des ventes faites hors ligne sur plusieurs postes. C'est volontaire : une panne doit être visible, claire, et traitée avec un processus papier.

## Ce que l'outil ne fait pas

L'outil ne couvre pas :

- portail vendeur en libre-service ;
- import Excel obligatoire ou persistance directe depuis un fichier Excel ;
- scanner code-barres obligatoire ;
- encaissement automatique par terminal bancaire ;
- intégration comptable ;
- portail de paiement ou comptabilité complète des frais vendeurs ;
- synchronisation hors ligne entre postes ;
- gestion de stock générique hors événement ;
- comptes utilisateurs individuels.

Ces limites sont des choix de simplicité. L'objectif de l'outil est d'avoir un flux fiable pour l'événement, pas de couvrir tous les cas d'un logiciel de gestion commerciale.

## Parcours de démonstration rapide

Pour présenter l'outil à des utilisateurs :

1. Montrer le statut visible en haut de page.
2. Ouvrir `VENDEURS`, créer ou ouvrir un vendeur.
3. Ajouter quelques articles : ils restent `enregistré`.
4. Ouvrir `Finaliser le dépôt`, montrer l'étape des frais vendeur, puis montrer `Imprimer le contrat` et la confirmation `Valider le dépôt` / `Le contrat est signé`.
5. Passer dans `CAISSE`, chercher un article, l'ajouter au panier et montrer le calcul du total.
6. Ouvrir `ADMIN`, expliquer le mot de passe et les trois statuts.
7. Passer en `Après-vente`.
8. Ouvrir `RAPPROCHEMENT`, imprimer une feuille vendeur.
9. Montrer le cas exceptionnel `Marquer manquant`, puis la réimpression de la feuille.

Le message principal à faire passer : les bénévoles utilisent des écrans simples, et les actions qui changent fortement le résultat de l'événement sont volontairement séparées et protégées.
