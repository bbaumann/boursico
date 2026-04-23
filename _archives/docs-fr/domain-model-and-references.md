# Modèle de domaine et références

## Modèle de domaine principal

Le projet repose sur un petit modèle opérationnel centré sur l'item, avec un rapprochement structuré autour du vendeur.

## Entités

### Seller

Représente la personne ou l'organisation qui apporte des jouets à vendre.

Champs suggérés :

- seller id
- first name
- last name
- seller code
- informations de contact selon le besoin

### Item

Représente un jouet physique.

Champs suggérés :

- item id
- seller id
- reference
- valeur de code-barres optionnelle si nécessaire plus tard
- title
- seller price
- sale price
- status

### Sale

Représente un passage en caisse côté client.

Champs suggérés :

- sale id
- timestamp
- total amount
- texte optionnel `receipt_details`

### SaleLine

Représente un item vendu à l'intérieur d'une `sale`.

Champs suggérés :

- sale line id
- sale id
- item id
- prix de vente au moment de la vente

## Modèle de statut d'item

La V1 utilise un seul champ `status`, volontairement simple :

- `available`
- `sold`
- `missing`

Significations dérivées :

- items payables = `sold` + `missing`
- items retournés = `available`

## Modèle de prix

Chaque `item` stocke deux prix :

- seller price : le montant payable au vendeur si l'item est payable
- sale price : le montant facturé au client

Règle métier actuelle :

- sale price = seller price + 20 pour cent

Le règlement vendeur en V1 utilise uniquement `seller price`.

## Modèle de reçu

La V1 utilise un seul champ texte optionnel :

- `receipt_details`

Signification :

- si `receipt_details` est nul ou vide, aucun reçu n'a été demandé
- si `receipt_details` est renseigné, un reçu a été demandé pour un besoin de remboursement ultérieur

Ce champ peut contenir le nom du client, l'organisation, l'adresse ou tout autre texte que l'opérateur doit imprimer sur le reçu.

La V1 ne suit pas séparément :

- un indicateur de demande de reçu
- un indicateur d'impression du reçu

## Schéma de référence

## Format final visible par l'opérateur

Les références utilisent le format suivant :

- `SELLERCODE-ITEMNO`

Exemple :

- `BK-014`

## Seller code

Les `seller_code` sont des codes à deux lettres attribués une seule fois par vendeur pour l'événement.

### Alphabet sécurisé

Utiliser des lettres majuscules en excluant :

- `I`
- `O`
- `Q`
- `V`

Alphabet sécurisé :

- `A B C D E F G H J K L M N P R S T U W X Y Z`

Cela donne 22 lettres et 484 `seller_code` possibles sur deux lettres.

### Ordre d'attribution des `seller_code`

Attribuer les `seller_code` dans un ordre réparti, en commençant par la première lettre.

Exemple de séquence de préfixes :

- `AA`
- `BA`
- `CA`
- `DA`
- ...
- `ZA`
- `AB`
- `BB`
- `CB`

Ce choix permet de répartir les vendeurs sur les premières lettres pour faciliter le tri physique post-vente.

## Numéro d'item chez le vendeur

Chaque vendeur reçoit une séquence à largeur fixe qui lui est propre :

- `001`
- `002`
- `003`

Les items ajoutés plus tard poursuivent simplement la séquence.

Exemple :

- `BK-001`
- `BK-002`
- `BK-003`

## Pourquoi ce schéma de référence a été choisi

- tous les items d'un vendeur se trient naturellement ensemble
- le tri post-vente devient plus simple
- il n'est plus nécessaire de conserver l'ancien système de trous dans une numérotation globale réservée
- les items ajoutés tardivement sont très simples à attribuer
- les références restent lisibles et faciles à manipuler à la main
- les références ne dépendent ni du nom du vendeur ni d'une logique de hash

## Sémantique des fiches dérivée du modèle

### Liste payable

Contient :

- les items avec le statut `sold`
- les items avec le statut `missing`

Les items manquants sont affichés dans la liste payable avec un marqueur visible `MISSING`.

### Liste retournée

Contient :

- les items avec le statut `available`

### Montant final à payer

Calculé comme :

- la somme des seller prices pour tous les items de la liste payable
