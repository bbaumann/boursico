# Périmètre et phasage

## Périmètre V1

La V1 est délibérément limitée.

### Inclus dans la V1

- création de `seller`
- saisie manuelle des `item` par les organisateurs
- un enregistrement `item` par jouet
- génération de références tenant compte du vendeur
- recherche le jour de vente par référence saisie avec autocomplete
- panier et calcul du total
- persistance des ventes
- impression optionnelle de reçu
- génération des fiches vendeur post-vente
- marquage d'un item invendu comme `missing`
- régénération des fiches vendeur après correction d'un item manquant
- processus de secours papier

### Explicitement hors périmètre pour la V1

- portail self-service vendeur
- import tableur comme workflow obligatoire
- dépendance à un scanner code-barres
- suivi des moyens de paiement
- intégration comptable
- automatisation des frais vendeur pour la règle des 2 EUR par 15 items
- logs client hors ligne et replay
- conflits de synchronisation multi-postes
- couverture fonctionnelle de type inventaire générique ou ERP

## Objectifs étendus

Ce sont des idées déjà identifiées, mais qui ne font pas partie de l'engagement V1 actuel :

- portail de préinscription vendeur
- UI de rapprochement plus pratique pour les organisateurs
- génération automatisée de documents plus formels au-delà du workflow de fiche vendeur
- outillage d'import plus large

## Pourquoi la V1 est volontairement petite

- l'événement est annuel, pas une activité de vente au détail quotidienne
- l'UI du jour de vente doit être très fiable pour des bénévoles non techniques
- la plus grande difficulté historique est la consolidation, pas la complexité du passage en caisse
- une première version plus petite réduit le risque opérationnel avant le prochain événement

## Trajectoire d'évolution probable

### Phase 1

Stabiliser les opérations de l'événement et obtenir des données fiables faisant autorité dans un seul système.

### Phase 2

Améliorer l'ergonomie du rapprochement et automatiser davantage la génération des documents vendeur.

### Phase 3

Ajouter éventuellement une préinscription vendeur si la complexité supplémentaire du workflow en vaut la peine.

## Critères de succès pour la V1

- les bénévoles peuvent tenir la caisse avec une formation minimale
- les ventes numériques n'exigent plus de consolidation multi-fichiers en fin de journée
- les fiches vendeur post-vente sont reproductibles et correctes
- la gestion des items manquants est prise en charge sans réécriture manuelle des documents
- la solution papier est documentée et opérationnellement claire
