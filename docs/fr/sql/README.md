# DDL SQL physique

Ce répertoire contient des brouillons concrets de DDL SQL pour le schéma logique V1 retenu.

Fichiers :

- [Schéma SQLite](../../sql/sqlite.sql)
- [Schéma PostgreSQL](../../sql/postgresql.sql)

## Objectif

Ces fichiers ont pour but de :

- rendre explicites les compromis entre moteurs
- permettre de démarrer rapidement l'implémentation sur SQLite ou PostgreSQL
- garder alignés le schéma logique et le schéma physique

## Notes importantes

## 1. Le DDL est volontairement conservateur

Les schémas appliquent :

- clés primaires
- clés étrangères
- contraintes d'unicité
- checks simples sur les statuts et les booléens
- checks élémentaires de cohérence des prix

Ils n'appliquent volontairement pas tous les invariants métier directement dans la base.

Certaines règles restent au niveau applicatif en V1, notamment :

- le moment où le statut d'un `item` passe à `sold`
- la correspondance entre le total d'une `sale` et la somme de ses `sale_line`
- la prévention de modifications non sûres une fois qu'un item a été vendu

## 2. Sémantique de la référence et du `seller_code`

La base stocke à la fois :

- le `seller_code`
- le numéro d'item local au vendeur
- la `reference` finale

L'application est censée générer et persister la `reference` finale plutôt que de la recalculer à chaque lecture.

## 3. Limites d'indexation pour l'autocomplete

Les index B-tree classiques sont utiles pour :

- la recherche exacte
- la recherche par préfixe
- les parcours triés

Ils ne résolvent pas totalement une recherche arbitraire par sous-chaîne comme `%lego%` sur de grands champs texte.

Pour la V1, le plan d'index actuel est acceptable si l'autocomplete est implémenté comme :

- une recherche par préfixe sur `reference`
- une recherche par préfixe ou une correspondance légère sur `title`

Si, plus tard, une recherche forte par sous-chaîne ou fuzzy devient nécessaire, les options probables sont :

- SQLite FTS
- indexes trigram PostgreSQL (`pg_trgm`)

## 4. Différences entre SQLite et PostgreSQL

### SQLite

- utilise `INTEGER PRIMARY KEY` pour les row ids
- utilise l'affinité `NUMERIC` pour les prix
- utilise des entiers `0/1` plus `CHECK` pour les booléens
- convient uniquement comme base du serveur central, pas comme architecture client à fichier partagé multi-writer

### PostgreSQL

- utilise `BIGINT GENERATED ALWAYS AS IDENTITY`
- utilise `BOOLEAN`
- utilise `NUMERIC(10,2)` pour les prix
- offre une trajectoire plus propre pour des contraintes plus fortes et une indexation plus riche dans le futur

## 5. Recommandation sur la frontière transactionnelle

Lors de l'enregistrement d'une `sale`, l'application doit effectuer les étapes suivantes dans une seule transaction :

1. revérifier que tous les items du panier sont encore `in_stock`
2. créer la `sale`
3. créer toutes les lignes `sale_line`
4. mettre à jour tous les `item.status` correspondants vers `sold`

La V1 ne réserve volontairement pas les items au moment de l'ajout au panier.

Si un item du panier n'est plus `in_stock`, l'application doit rejeter clairement l'enregistrement et laisser l'opérateur recommencer après avoir retiré l'item en conflit.

Cette frontière transactionnelle est plus importante, en V1, que l'ajout d'une logique astucieuse côté base de données.
