# Fonctionnalités

## Découpage des fonctionnalités

Le système est volontairement découpé en deux fonctionnalités indépendantes partageant une base de données commune :

1. les opérations de l'événement
2. le rapprochement post-vente

Ce découpage est important, car les utilisateurs, les besoins d'UI et les risques opérationnels sont différents.

## Opérations de l'événement

### Objectif

Permettre aux organisateurs d'enregistrer les jouets avant l'événement et aux bénévoles du jour de vente de créer des ventes de manière fiable, avec la charge cognitive la plus faible possible.

### Capacités V1

- créer des `seller`
- créer manuellement des `item` à partir des listes fournies par les vendeurs
- attribuer une référence unique à chaque item
- stocker `seller_price` et `sale_price` pour chaque item
- rechercher des items par `reference` via un autocomplete au clavier
- ajouter les items sélectionnés à un panier
- calculer automatiquement le total du ticket
- enregistrer une `sale` comme payée
- imprimer un reçu en option

### Contraintes UX du jour de vente

- aucune information vendeur dans le flux de passage en caisse
- aucun suivi du moyen de paiement au-delà du fait que la vente est terminée
- aucun scanner code-barres requis en V1
- la recherche doit accepter une saisie partielle de la référence avec des suggestions rapides
- l'opérateur doit sélectionner explicitement l'item dans la liste proposée avant qu'il soit ajouté au panier

### Flux de passage en caisse attendu

1. L'opérateur saisit une partie ou la totalité de la référence du jouet.
2. Les items correspondants sont proposés dans une liste très contrastée.
3. L'opérateur sélectionne le bon item.
4. L'item est ajouté au panier en cours.
5. L'opérateur répète l'opération jusqu'à ce que le client ait terminé.
6. L'opérateur enregistre la `sale`.
7. L'opérateur imprime éventuellement un reçu dans les rares cas de remboursement.

## Rapprochement post-vente

### Objectif

Produire, après la fin de la vente, les documents par vendeur nécessaires au regroupement physique des items et au paiement vendeur.

### Capacités V1

- lire les données d'`item` et de `sale` faisant autorité depuis la base partagée
- regrouper les items par `seller`
- calculer les items payables et retournés par vendeur
- générer une fiche vendeur imprimable par vendeur
- permettre de marquer un item invendu comme `missing`
- régénérer la fiche vendeur après correction d'un item manquant

### Contenu de la fiche vendeur

La fiche vendeur doit être lisible et imprimable.

Elle doit contenir :

- l'identité du vendeur
- la liste des items payables
- la liste des items retournés
- le montant final à payer au vendeur

### Liste des items payables

La liste payable contient :

- les items vendus
- les items manquants

Pour chaque item payable, afficher :

- la `reference`
- une courte description si utile
- le `seller_price`
- un marqueur visible `MISSING` si nécessaire

### Liste des items retournés

La liste retournée contient tous les items encore marqués `available`.

Pour chaque item retourné, afficher :

- la `reference`
- une courte description si utile

### Montant final à payer

En V1, le montant final à payer au vendeur est :

- la somme des `seller_price` de tous les items de la liste payable

Les éléments suivants restent hors système en V1 :

- le frais de 2 EUR par 15 items
- tout calcul de frais visible côté vendeur

### Workflow physique pris en charge par la fiche

1. Un opérateur utilise la fiche pour rassembler tous les items retournés.
2. Si un item retourné ne peut pas être retrouvé physiquement, un administrateur le marque `missing`.
3. La fiche est régénérée.
4. Un autre opérateur utilise la fiche finale pour préparer le chèque vendeur.
5. Le chèque, la fiche finale et les items retournés sont regroupés physiquement pour le retrait vendeur.

## Non-objectifs de la V1

- portail self-service vendeur
- gestion de stock générique au-delà des besoins de l'événement
- flux intégrés de terminal de paiement
- fonctionnalités ERP ou comptables complexes
- synchronisation hors ligne multi-postes
- gestion intégrée des frais vendeur de 2 EUR par 15 items
