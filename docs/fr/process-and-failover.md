# Processus et continuité en cas de panne

## Processus normal

### Avant l'événement

1. Les vendeurs envoient leurs listes d'items en dehors du système, généralement par email.
2. L'opérateur de saisie crée les `seller` dans le système.
3. L'opérateur de saisie crée un `item` par jouet.
4. Les références sont générées pour chaque item.
5. Les items sont préparés pour l'événement.

### Pendant la vente

Avant le premier passage en caisse :

- les données métier des items sont gelées pour l'événement
- seuls les changements de statut autorisés restent possibles à partir de ce moment

1. Le bénévole du jour de vente saisit une partie ou la totalité d'une référence.
2. Les items correspondants apparaissent.
3. Le bénévole sélectionne le bon item.
4. L'item est ajouté au panier.
5. Le bénévole enregistre la `sale` lorsque le client a terminé.
6. Le serveur revalide que tous les items du panier sont encore `in_stock`.
7. Si la validation réussit, la `sale` est enregistrée et ces items deviennent `sold`.
8. Si un item n'est plus `in_stock`, l'enregistrement est rejeté et l'opérateur doit retirer l'item en conflit avant de recommencer.
9. Le reçu est imprimé uniquement sur demande.

### Après la vente

1. L'outil de rapprochement génère une fiche vendeur pour chaque vendeur.
2. Un opérateur regroupe physiquement tous les items retournés du stock à l'aide de la fiche.
3. Si un item retourné ne peut pas être retrouvé, l'administrateur le marque `missing`.
4. La fiche vendeur est régénérée.
5. Un autre opérateur rédige le chèque en fonction du montant final à payer.
6. Le chèque, la fiche finale vendeur et les items retournés sont regroupés physiquement pour le retrait vendeur.

## Workflow de la fiche vendeur

### Cas par défaut

Si aucun item n'est manquant :

- la première fiche vendeur générée est déjà la fiche vendeur finale

### Cas exceptionnel

Si un item listé comme retourné ne peut pas être retrouvé physiquement :

1. L'administrateur met à jour le statut de l'item de `available` vers `missing`.
2. La fiche vendeur est régénérée.
3. L'item passe de la liste retournée à la liste payable et est marqué `MISSING`.

## Pannes opérationnelles

## Philosophie de gestion des pannes pour la V1

La V1 préfère volontairement :

- une panne claire
- une solution manuelle
- un rapprochement ultérieur

plutôt que :

- un comportement hors ligne partiel et caché
- la complexité d'un replay local
- la gestion de conflits distribués

## Mode de panne numérique officiel

Si le serveur central est indisponible :

- les ventes numériques s'arrêtent
- les opérateurs basculent sur des tickets papier
- le rapprochement est effectué manuellement plus tard

Il n'y a pas de replay local par poste en V1.

## Procédure de secours papier

### Matériel préparé

- carnets de tickets papier pré-numérotés
- stylos et planchettes
- consignes écrites de bascule

### Procédure

1. Noter l'heure de la panne.
2. Arrêter d'utiliser le système numérique.
3. Faire basculer tous les postes de vente sur des tickets papier numérotés.
4. Conserver ensemble et dans l'ordre tous les tickets papier complétés.
5. Continuer l'événement manuellement.
6. Après l'événement, rapprocher les ventes papier avec le stock physique restant.

## Principe de récupération après crash majeur

Si le serveur est physiquement perdu et qu'une récupération numérique n'est pas réalisable à temps pour l'événement :

- utiliser la solution papier pour le reste de l'événement
- utiliser le stock physique à la fin de l'événement comme base faisant autorité pour le rapprochement manuel

Sur le plan opérationnel, tout ce qui n'est pas présent dans le stock physique et n'est pas retourné peut être traité comme vendu ou payable selon les règles de rapprochement de secours utilisées par les organisateurs.

## Mesures de fiabilité à conserver même en V1

- sauvegardes périodiques automatiques de la base de données
- sauvegarde avant l'ouverture des ventes
- sauvegarde après la clôture des ventes
- export des listes de ventes et d'items si c'est pratique
- consignes écrites de bascule pour les opérateurs

## Exigences d'auditabilité

- chaque `sale` doit être persistée immédiatement
- la sélection d'un item dans un panier ne doit pas créer d'état de réservation caché
- l'enregistrement d'une `sale` doit revérifier la disponibilité courante des items avant persistance
- la disponibilité d'un `item` doit empêcher une double vente après confirmation
- les fiches vendeur doivent toujours pouvoir être reproduites à partir des données courantes de la base
- les modifications d'item manquant doivent pouvoir être attribuées à un utilisateur ou à une action administrateur lorsque c'est possible
