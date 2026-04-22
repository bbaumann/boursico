# Cas d'usage et personas

## Personas

### Bénévole du jour de vente

Profil :

- non technique
- souvent âgé
- intervient uniquement pendant l'événement
- a besoin d'une interface très claire et d'une formation minimale

Objectif principal :

- saisir des références d'item et enregistrer les ventes client de manière fiable

Ce dont cette personne n'a pas besoin :

- informations vendeur
- concepts comptables
- administration d'inventaire
- gestion des moyens de paiement

### Opérateur de saisie

Profil :

- plus à l'aise avec l'outil informatique
- prépare les données de l'événement avant la vente

Objectif principal :

- créer des `seller` et des `item` à partir des listes fournies par les vendeurs

### Administrateur de rapprochement

Profil :

- plus technique ou plus attentif au détail que les bénévoles du jour de vente
- travaille une fois la vente terminée
- rôle opérationnel plutôt qu'identité logicielle nominative

Objectifs principaux :

- générer les fiches vendeur
- marquer les items manquants lorsqu'ils sont identifiés
- régénérer les fiches finales

Modèle d'accès en V1 :

- les changements vers `missing` exigent le mot de passe admin partagé
- il s'agit d'un contrôle léger de prévention des abus, pas d'un vrai système utilisateurs/rôles

### Opérateur de paiement

Profil :

- utilisateur opérationnel après l'événement
- peut ne pas être technique

Objectif principal :

- utiliser la fiche vendeur finale pour rédiger les chèques et préparer les sacs de retrait vendeur

## Cas d'usage

### UC-01 Créer un `seller`

Acteur :

- opérateur de saisie

Description :

- Créer un enregistrement `seller` pour l'événement.

Résultat attendu :

- Le vendeur existe et reçoit un `seller_code` attribué pour la génération des références.

### UC-02 Créer un `item`

Acteur :

- opérateur de saisie

Description :

- Créer un `item` par jouet physique, lié à un vendeur.

Résultat attendu :

- L'item a une `reference` unique, un `seller_price`, un `sale_price` et un statut initial `available`.

### UC-03 Rechercher un `item` pendant la vente

Acteur :

- bénévole du jour de vente

Description :

- Saisir tout ou partie d'une `reference` et choisir l'item voulu dans la liste correspondante.

Résultat attendu :

- Le bon item est sélectionné sans avoir besoin du contexte vendeur.

### UC-04 Ajouter un `item` au panier

Acteur :

- bénévole du jour de vente

Description :

- Ajouter l'item sélectionné au panier client en cours.

Résultat attendu :

- L'item apparaît dans le panier avec son prix et contribue au total.

### UC-05 Enregistrer une `sale`

Acteur :

- bénévole du jour de vente

Description :

- Confirmer la vente lorsque le client a terminé ses achats.

Résultat attendu :

- La `sale` est persistée, les items concernés ne sont plus disponibles et l'écran est réinitialisé pour le client suivant.

### UC-06 Imprimer un reçu

Acteur :

- bénévole du jour de vente

Description :

- Imprimer un reçu en option dans le cas rare d'un remboursement.
- L'opérateur ne remplit un champ texte libre de reçu que lorsque le client demande un justificatif pour remboursement.

Résultat attendu :

- Le reçu est produit sans compliquer le flux normal de passage en caisse.

### UC-07 Générer les fiches vendeur

Acteur :

- administrateur de rapprochement

Description :

- Générer une fiche vendeur imprimable une fois la vente terminée.

Résultat attendu :

- Chaque fiche affiche les items payables, les items retournés et le montant final à payer.

### UC-08 Marquer un `item` manquant

Acteur :

- administrateur de rapprochement

Description :

- Faire passer un `item` de `available` à `missing` lorsqu'il ne peut pas être retrouvé physiquement pendant le regroupement post-vente.
- Cette action exige le mot de passe admin partagé en V1.

Résultat attendu :

- La fiche vendeur régénérée déplace l'item de la liste retournée vers la liste payable avec un marqueur `MISSING`.

### UC-09 Préparer le sac de retrait vendeur

Acteur :

- opérateur de paiement

Description :

- Utiliser la fiche finale pour préparer le chèque du vendeur et son sac physique.

Résultat attendu :

- Le sac contient la fiche vendeur finale, les items retournés et le chèque.

## Implications UX issues des personas

- Le flux du jour de vente doit privilégier la vitesse, la lisibilité et l'absence d'ambiguïté.
- Le système doit éviter d'exposer des données inutiles pendant les ventes.
- Le rapprochement peut tolérer un workflow plus technique, car il est utilisé plus tard par d'autres personnes.
