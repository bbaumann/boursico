# Cycle de vie des statuts d'item

## Objectif

Ce document résume le modèle de statut V1 des items et les changements de statut autorisés selon les phases de l'événement.

Il définit aussi la règle de mutabilité opérationnelle introduite à l'ouverture des ventes :

- avant l'ouverture des ventes, l'équipe de saisie peut encore corriger les données métier des items
- une fois le passage en caisse ouvert, les données métier des items deviennent en lecture seule en fonctionnement normal
- après l'ouverture des ventes, seuls les changements de statut autorisés ci-dessous restent modifiables

## Données item gelées à l'ouverture des ventes

Quand le passage en caisse ouvre pour l'événement, les champs item suivants deviennent en lecture seule en fonctionnement normal :

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price`
- `sale_price`

Le champ `status` reste modifiable uniquement via les transitions de workflow définies dans ce document.

## Résumé des statuts

### `registered`

- preregistré à partir d'une liste vendeur reçue par email
- pas encore livré physiquement
- exclu du passage en caisse et des sorties de fiche vendeur

### `in_stock`

- livré physiquement et présent dans le stock de l'association
- éligible à la vente pendant l'événement
- traité comme retourné après la vente sauf s'il est plus tard marqué `missing`

### `not_provided`

- preregistré mais jamais livré physiquement
- exclu du passage en caisse et des sorties de fiche vendeur

### `sold`

- vendu pendant le passage en caisse
- payable au vendeur

### `missing`

- attendu comme item à rendre après la vente mais introuvable physiquement
- payable au vendeur et marqué `MISSING` sur la fiche vendeur

## Transitions autorisées

### Avant l'ouverture des ventes

- créer directement un item en `in_stock` pendant la saisie en personne
- créer un item en `registered` depuis une preregistration email
- `registered -> in_stock` lors de la confirmation de livraison physique
- `registered -> not_provided` lors de la confirmation de livraison physique

### Pendant la vente

- `in_stock -> sold` lorsqu'une `sale` est enregistrée

Il n'existe pas de statut de réservation en V1 :

- l'ajout d'un item à un panier ne change pas son statut
- le serveur revalide le statut `in_stock` lorsque la `sale` est enregistrée
- si la validation échoue, la `sale` est rejetée et aucun changement partiel de statut n'est appliqué

### Après la vente

- `in_stock -> missing` pendant le rapprochement post-vente

## Statuts terminaux en V1

Les statuts suivants sont terminaux en fonctionnement normal V1 :

- `not_provided`
- `sold`
- `missing`

Aucune transition de retour depuis ces statuts n'est définie en V1.

## Transitions interdites en V1

- `registered -> sold`
- `registered -> missing`
- `not_provided -> in_stock`
- `sold -> in_stock`
- `sold -> missing`
- `missing -> in_stock`

Si une erreur opérateur est découverte, la correction doit passer par une procédure administrateur explicite et non par des changements d'état ad hoc dans l'UI standard.

## Résumé par rôle

### Opérateur de saisie

Avant l'ouverture des ventes :

- créer des items `registered` à partir des listes email
- créer des items `in_stock` quand les jouets sont physiquement présents
- confirmer la livraison des preregistrations vers `in_stock` ou `not_provided`
- corriger les données métier des items si nécessaire

### Bénévole du jour de vente

Pendant la vente :

- faire passer les items `in_stock` à `sold` uniquement en enregistrant une `sale`
- ne peut pas modifier les données métier des items

### Administrateur de rapprochement

Après la vente :

- faire passer les items `in_stock` à `missing` lorsqu'un retour physique attendu est introuvable
- ne peut pas rouvrir ni réécrire les données métier des items dans le workflow normal de rapprochement

## Références croisées

- [Fonctionnalités](./features.md)
- [Processus et continuité en cas de panne](./process-and-failover.md)
- [Schéma de base de données](./database-schema.md)
