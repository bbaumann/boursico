# Schéma de base de données

## Objectif

Ce document définit le schéma logique V1 de la base de données pour l'outil de vente de jouets de l'association.

Il est volontairement agnostique vis-à-vis du moteur :

- aucun dialecte SQL n'est supposé à ce stade
- le modèle doit pouvoir se mapper proprement vers SQLite sur un serveur central ou vers PostgreSQL plus tard

Le schéma est conçu comme un modèle relationnel normalisé minimal.

Des brouillons de DDL concrets sont disponibles dans :

- [DDL SQLite](../sql/sqlite.sql)
- [DDL PostgreSQL](../sql/postgresql.sql)
- [Notes DDL](./sql/README.md)

## Principes de conception

- Un jouet physique correspond à un enregistrement `item`.
- Un passage en caisse terminé correspond à une `sale`.
- Un jouet vendu apparaît dans exactement une `sale_line`.
- Les fiches vendeur sont dérivées des données courantes et ne sont pas stockées comme objets métier principaux en V1.
- Le schéma prend logiquement en charge plusieurs événements, même si la V1 peut fonctionner opérationnellement avec un événement par base de données.

## Tables

## `event`

Représente un événement de vente.

### Colonnes

- `id`
- `code`
- `name`

### Notes

- `code` est un identifiant court tel que `2026`.
- Aucun champ de date d'événement ni de statut n'est inclus en V1.

## `seller`

Représente un vendeur pour un événement.

### Colonnes

- `id`
- `event_id`
- `seller_code`
- `first_name`
- `last_name`
- `email`
- `phone`
- `notes`

### Notes

- `seller_code` est le code à deux lettres utilisé dans les références d'`item`.
- Aucun `display_name` stocké n'est nécessaire ; il peut être dérivé si besoin.

## `item`

Représente un jouet physique.

### Colonnes

- `id`
- `event_id`
- `seller_id`
- `reference`
- `item_number`
- `title`
- `seller_price`
- `sale_price`
- `status`

### Notes

- `reference` est le code visible par l'utilisateur, par exemple `BK-014`.
- `item_number` est la séquence interne locale au vendeur utilisée pour construire la référence.
- `title` est le libellé de l'item affiché dans les résultats de recherche, les paniers et les fiches.
- `status` utilise le modèle de statut V1 : `available`, `sold`, `missing`.

## `sale`

Représente un passage en caisse terminé.

### Colonnes

- `id`
- `event_id`
- `sold_at`
- `total_amount`
- `receipt_details`

### Notes

- Il n'y a pas de champ `is_paid` en V1 ; persister la `sale` signifie que l'encaissement est terminé.
- `receipt_details` est un champ texte libre optionnel utilisé uniquement dans le cas rare d'un remboursement.
- Si `receipt_details` est nul, aucun reçu n'a été demandé en V1.

## `sale_line`

Représente un item vendu à l'intérieur d'une `sale`.

### Colonnes

- `id`
- `sale_id`
- `item_id`
- `seller_price_at_sale`
- `sale_price_at_sale`

### Notes

- Les instantanés de prix sont stockés sur la `sale_line` afin de protéger le rapprochement si les données de l'item sont modifiées plus tard par erreur.
- Un `item` peut apparaître dans au plus une `sale_line`.

## Relations

- un `event` possède plusieurs `seller`
- un `event` possède plusieurs `item`
- un `event` possède plusieurs `sale`
- un `seller` possède plusieurs `item`
- une `sale` possède plusieurs `sale_line`
- un `item` possède au plus une `sale_line`

## Clés et contraintes

## Clés primaires

- `event.id`
- `seller.id`
- `item.id`
- `sale.id`
- `sale_line.id`

## Clés étrangères

- `seller.event_id -> event.id`
- `item.event_id -> event.id`
- `item.seller_id -> seller.id`
- `sale.event_id -> event.id`
- `sale_line.sale_id -> sale.id`
- `sale_line.item_id -> item.id`

## Contraintes d'unicité

- `event.code` unique
- `(seller.event_id, seller.seller_code)` unique
- `(item.event_id, item.reference)` unique
- `(item.seller_id, item.item_number)` unique
- `sale_line.item_id` unique

## Nullabilité

## `event`

- `code` : obligatoire
- `name` : obligatoire

## `seller`

- `event_id` : obligatoire
- `seller_code` : obligatoire
- `first_name` : obligatoire
- `last_name` : obligatoire
- `email` : optionnel
- `phone` : optionnel
- `notes` : optionnel

## `item`

- `event_id` : obligatoire
- `seller_id` : obligatoire
- `reference` : obligatoire
- `item_number` : obligatoire
- `title` : obligatoire
- `seller_price` : obligatoire
- `sale_price` : obligatoire
- `status` : obligatoire

## `sale`

- `event_id` : obligatoire
- `sold_at` : obligatoire
- `total_amount` : obligatoire
- `receipt_details` : optionnel

## `sale_line`

- `sale_id` : obligatoire
- `item_id` : obligatoire
- `seller_price_at_sale` : obligatoire
- `sale_price_at_sale` : obligatoire

## Énumérations

## Statut d'item

Valeurs autorisées :

- `available`
- `sold`
- `missing`

Signification métier :

- `available` : l'item doit être rendu au vendeur
- `sold` : l'item a été vendu pendant l'événement
- `missing` : l'item n'a pas été retrouvé pendant le regroupement post-vente et doit donc être payé au vendeur

## Règles de suppression et de mise à jour

La V1 doit rester conservatrice et préférer la restriction à la suppression en cascade.

## Règles de suppression

- ne pas supprimer un `event` une fois que des données métier existent
- ne pas supprimer un `seller` si des `item` existent pour ce vendeur
- ne pas supprimer un `item` s'il est lié à une `sale_line`
- ne pas supprimer une `sale` en fonctionnement normal une fois créée
- ne pas supprimer de `sale_line` en fonctionnement normal

## Règles de mise à jour

- `seller.seller_code` ne doit pas changer une fois que des `item` existent pour ce vendeur
- avant l'ouverture des ventes, l'équipe de saisie peut encore corriger les données métier des items si nécessaire
- une fois le passage en caisse ouvert, les données métier des items deviennent en lecture seule en fonctionnement normal
- après l'ouverture des ventes, `item.reference`, `item.item_number`, `item.seller_id`, `item.title`, `item.seller_price` et `item.sale_price` ne doivent plus changer
- après l'ouverture des ventes, seuls les changements de statut pilotés par le workflow restent autorisés en V1
- `sale.total_amount` doit être traité comme immuable après sa création
- les instantanés de `sale_line` sont immuables

## Invariants métier

Ce sont des invariants importants même s'ils sont d'abord appliqués dans la logique applicative plutôt que comme contraintes fortes en base.

- l'ajout d'un item à un panier ne le réserve pas en V1
- une `sale` ne peut être persistée que si tous les items du panier sont encore `in_stock` au moment de la transaction
- si un `item` apparaît dans `sale_line`, son `status` doit être `sold`
- un `item` ne peut être vendu qu'une seule fois
- `sale.total_amount` doit être égal à la somme des `sale_price_at_sale` de ses lignes
- `item.reference` doit correspondre au `seller_code` de l'événement et au numéro local vendeur utilisés pour la générer

## Signification métier dérivée

Les sorties suivantes sont dérivées du schéma et ne sont pas stockées comme tables V1 séparées.

## Items payables par vendeur

Tous les `item` d'un vendeur ayant :

- `status = sold`
- ou `status = missing`

Les items manquants doivent apparaître dans la liste payable avec une mention visible `MISSING`.

## Items retournés par vendeur

Tous les `item` du vendeur ayant :

- `status = available`

## Montant final à payer au vendeur

Pour la V1 :

- somme de `seller_price` pour tous les items payables

Le frais vendeur de 2 EUR par 15 items reste hors système en V1.

## Plan d'indexation

Indexes recommandés :

- index unique sur `event.code`
- index unique sur `(seller.event_id, seller.seller_code)`
- index unique sur `(item.event_id, item.reference)`
- index unique sur `(item.seller_id, item.item_number)`
- index unique sur `sale_line.item_id`
- index sur `item.reference`
- index sur `item.title`
- index sur `item.seller_id`
- index sur `item.status`
- index sur `(sale.event_id, sale.sold_at)`
- index sur `sale_line.sale_id`

Ils permettent :

- un autocomplete rapide sur la référence et le titre d'item
- la génération des fiches vendeur
- le filtrage post-vente par statut d'item
- la liste et l'export des ventes au niveau événement

## Génération des références

Le schéma suppose que les références sont générées à partir de :

- `seller.seller_code`
- `item.item_number`

Format final visible par l'opérateur :

- `SELLERCODE-NNN`

Exemple :

- `BK-014`

Règle de génération :

1. attribuer le code vendeur lors de la création du `seller`
2. attribuer le prochain `item.item_number` local au vendeur lors de la création d'un nouvel `item`
3. construire et stocker la référence finale

## Notes sur les moteurs

Ce schéma logique est volontairement portable.

### Compatibilité SQLite

- convient s'il est utilisé uniquement comme base du serveur central
- suffit pour le volume et les contraintes de la V1
- nécessite une discipline côté application pour les invariants métier et la gestion des migrations

### Compatibilité PostgreSQL

- offre une trajectoire plus propre à long terme pour des contraintes plus fortes, une indexation plus riche et la croissance future
- deviendra probablement le meilleur choix par défaut dès que la complexité de déploiement sera acceptable

Le schéma lui-même ne doit bloquer aucun de ces choix.
