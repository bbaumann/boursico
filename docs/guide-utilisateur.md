# Guide utilisateur - Outil de vente de l'association

Ce guide explique l'outil du point de vue des organisateurs, des bénévoles et des personnes qui préparent le rapprochement après la vente. Il est volontairement plus simple que la documentation technique : il décrit les usages, les écrans importants et la logique de sécurité opérationnelle.

## Idée générale

L'outil sert à gérer une vente annuelle de jouets pour une association.

Il remplace les fichiers Excel et les consolidations manuelles de fin de journée par une seule base de données centrale. Chaque jouet physique correspond à un article dans l'application. Chaque vente est enregistrée immédiatement. Après la vente, les feuilles vendeurs sont générées à partir des faits enregistrés dans l'outil.

Le but n'est pas de créer un logiciel de caisse complet ni un ERP. Le but est de rendre l'événement plus sûr, plus simple et plus facile à vérifier.

## Philosophie de l'outil

### Simple pendant la vente

Le jour de la vente, les bénévoles doivent pouvoir encaisser avec très peu de formation. L'écran de caisse ne montre pas les informations vendeurs, ne demande pas le mode de paiement, ne dépend pas d'un scanner, et ne demande pas de gérer des notions comptables.

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

V1 n'a pas de comptes utilisateurs individuels. Certaines actions sont simplement protégées par un mot de passe administrateur partagé :

- changer le statut de l'événement ;
- corriger une ligne de vente ;
- ouvrir le rapprochement ;
- marquer un article comme manquant ;
- remettre en stock un article marqué manquant par erreur.

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

- cherche les articles par référence ou par titre ;
- ajoute les articles au panier ;
- finalise la vente ;
- imprime éventuellement un reçu si le client le demande.

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

Renseigner :

- prénom ;
- nom ;
- e-mail si disponible ;
- téléphone ;
- adresse postale ;
- notes si nécessaire.

À l'enregistrement, l'outil attribue un code vendeur. Ce code sert ensuite à construire les références des articles.

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

### Ajouter des articles

Ouvrir la fiche du vendeur, puis utiliser la section `Ajouter des articles`.

Pour chaque article, saisir :

- le titre ;
- le prix vendeur en euros.

Le prix vendeur est aussi le prix imprimé vu par le client dans V1. Il sert aussi au calcul du montant à reverser au vendeur si l'article est vendu ou marqué manquant.

### Choisir le mode de saisie

Deux modes existent :

- `Dépôt` : les jouets sont physiquement présents. Les articles sont créés directement `en stock`.
- `Enregistrement` : les articles viennent d'une liste reçue à l'avance, par exemple par e-mail. Les articles sont créés `enregistré`, car ils ne sont pas encore physiquement déposés.

### Confirmer un dépôt après enregistrement

Quand un vendeur avait envoyé une liste à l'avance puis apporte les jouets :

1. Ouvrir sa fiche vendeur.
2. Relire les articles un par un.
3. Pour chaque article réellement apporté, cliquer sur `Marquer en stock`.
4. Pour les articles finalement non apportés, supprimer l'article tant qu'il est encore `enregistré`.

Un article `enregistré` n'apparaît ni en caisse ni dans les feuilles vendeurs finales.

## Pendant la vente : encaisser un client

Aller dans `CAISSE` ou ouvrir `/caisse`.

### Renseigner l'acheteur

Avant de finaliser une vente, remplir les champs `Nom ou structure`, `Code postal` et `Ville`.

Pour les codes postaux de l'Isère, la ville est proposée automatiquement et peut être modifiée si nécessaire. Pour les autres départements, saisir la ville librement.

### Chercher un article

Dans `Rechercher un article`, taper une partie de la référence ou du titre.

La table se filtre immédiatement. Le bénévole choisit explicitement la bonne ligne. Il n'y a pas besoin de connaître le vendeur.

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
3. Cliquer sur `Enregistrer la vente`.

L'application vérifie les articles avant d'afficher la fenêtre finale, puis revérifie au moment exact de l'enregistrement. Cela évite de vendre deux fois le même article si deux caisses travaillent en même temps.

Si un article n'est plus disponible, la vente est refusée clairement. Le bénévole doit retirer l'article en conflit du panier, puis recommencer la finalisation.

### Imprimer un reçu

Le reçu est optionnel.

Si le client demande un reçu ou une facture, remplir `Adresse` avec l'adresse à imprimer.

Ensuite, dans la fenêtre de finalisation, le bouton `Enregistrer et imprimer` devient disponible. L'application enregistre d'abord la vente, puis ouvre l'impression du navigateur.

Si le champ de reçu est vide, l'option d'impression reste désactivée.

## Administrer le statut de la vente

Aller dans `ADMIN` ou `/vente/statut`.

La page demande le `Mot de passe administrateur`. Une fois déverrouillée dans le navigateur, elle permet de changer le statut sans redemander le mot de passe à chaque clic.

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

Aller dans `ADMIN`, déverrouiller la page, puis utiliser `Correction des ventes`.

1. Filtrer les ventes par référence, titre, vendeur ou numéro de vente.
2. Trouver la mauvaise ligne.
3. Cliquer sur `Remettre en stock`.
4. Confirmer l'action.

Résultat :

- l'article revient `en stock` ;
- il peut être vendu à nouveau ;
- la ligne de vente d'origine reste marquée comme corrigée ;
- les totaux de la vente d'origine ne sont pas recalculés ;
- la ligne corrigée ne compte plus dans le paiement vendeur.

Cette règle évite de réécrire l'historique de caisse tout en corrigeant le stock et le futur rapprochement.

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

Les articles à rendre sont les articles encore `en stock`.

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
2. Filtrer les articles par référence, titre ou vendeur.
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

V1 ne calcule pas les frais vendeurs du type `2 EUR par 15 articles`. Si l'association applique cette règle, elle reste traitée hors application pour le moment.

## En cas de panne

Si le serveur central ou l'application n'est plus disponible pendant l'événement :

1. Noter l'heure de la panne.
2. Arrêter la vente numérique.
3. Passer aux factures papier pré-numérotées.
4. Garder les factures papier terminées ensemble et dans l'ordre.
5. Continuer l'événement manuellement.
6. Faire le rapprochement après l'événement à partir des factures papier et du stock physique restant.

L'outil ne tente pas de rejouer plus tard des ventes faites hors ligne sur plusieurs postes. C'est volontaire : pour cette V1, une panne doit être visible, claire, et traitée avec un processus papier.

## Ce que l'outil ne fait pas en V1

L'outil ne couvre pas :

- portail vendeur en libre-service ;
- import Excel obligatoire ou persistance directe depuis un fichier Excel ;
- scanner code-barres obligatoire ;
- suivi du mode de paiement ;
- intégration comptable ;
- calcul automatique des frais vendeurs ;
- synchronisation hors ligne entre postes ;
- gestion de stock générique hors événement ;
- comptes utilisateurs individuels.

Ces limites sont des choix de simplicité. L'objectif V1 est d'avoir un flux fiable pour l'événement, pas de couvrir tous les cas d'un logiciel de gestion commerciale.

## Parcours de démonstration rapide

Pour présenter l'outil à des utilisateurs :

1. Montrer le statut visible en haut de page.
2. Ouvrir `VENDEURS`, créer ou ouvrir un vendeur.
3. Ajouter quelques articles avec les modes `Dépôt` et `Enregistrement`.
4. Montrer qu'un article `enregistré` doit être confirmé avec `Marquer en stock`.
5. Passer dans `CAISSE`, chercher un article, l'ajouter au panier et montrer le calcul du total.
6. Ouvrir `ADMIN`, expliquer le mot de passe et les trois statuts.
7. Passer en `Après-vente`.
8. Ouvrir `RAPPROCHEMENT`, imprimer une feuille vendeur.
9. Montrer le cas exceptionnel `Marquer manquant`, puis la réimpression de la feuille.

Le message principal à faire passer : les bénévoles utilisent des écrans simples, et les actions qui changent fortement le résultat de l'événement sont volontairement séparées et protégées.
