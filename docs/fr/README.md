# Outil de vente de jouets pour association

## Objectif

Cette documentation rassemble la compréhension actuelle du projet pour :

- le ou les développeurs principaux
- tout développeur qui rejoindrait le projet plus tard
- de futures sessions d'IA ayant besoin du contexte du projet

Le projet concerne un événement annuel organisé par une association qui vend des jouets pour le compte de vendeurs.

La première version reste volontairement limitée :

- rendre la saisie initiale gérable
- rendre les opérations du jour de vente simples et sûres pour des bénévoles non techniques
- supprimer la difficulté historique de consolidation Excel/VBA en fin de journée
- garder le rapprochement post-vente indépendant des opérations du jour de vente

## Plan des documents

- [Fonctionnalités](./features.md)
- [Architecture](./architecture.md)
- [Schéma de base de données](./database-schema.md)
- [DDL SQL physique](./sql/README.md)
- [Cas d'usage et personas](./use-cases-and-personas.md)
- [Décisions](./decisions.md)
- [Processus et continuité en cas de panne](./process-and-failover.md)
- [Modèle de domaine et références](./domain-model-and-references.md)
- [Périmètre et phasage](./scope-and-phasing.md)

## Résumé du projet

L'outil est découpé en deux fonctionnalités indépendantes qui partagent une même base de données :

1. les opérations de l'événement
2. le rapprochement post-vente

Les opérations de l'événement sont optimisées pour des bénévoles âgés et non techniques pendant le jour de vente.

Le rapprochement post-vente est optimisé pour l'exactitude et l'auditabilité. Dans un premier temps, il peut être réalisé sous la forme d'un outil technique ou d'un script séparé plutôt que d'une UI complète.

## Principes clés

- Un jouet physique correspond à un seul enregistrement `item`.
- Une base de données centrale constitue l'unique source numérique de vérité.
- L'UX du jour de vente doit ressembler à un appareil dédié, pas à une application d'administration.
- Le rapprochement intervient une fois les ventes terminées et peut être traité comme une fonctionnalité distincte.
- En V1, la simplicité est préférée à l'ingéniosité.

## Orientation actuelle de la V1

- Outil sur mesure, non basé sur Odoo
- Déploiement sur réseau local
- Serveur central uniquement, sans synchronisation hors ligne entre les postes
- Solution papier si le serveur est indisponible
- Self-service vendeur hors périmètre pour la V1

## Ordre de lecture conseillé

Pour une personne qui découvre le projet, l'ordre de lecture recommandé est :

1. [Périmètre et phasage](./scope-and-phasing.md)
2. [Fonctionnalités](./features.md)
3. [Modèle de domaine et références](./domain-model-and-references.md)
4. [Schéma de base de données](./database-schema.md)
5. [DDL SQL physique](./sql/README.md)
6. [Architecture](./architecture.md)
7. [Processus et continuité en cas de panne](./process-and-failover.md)
8. [Décisions](./decisions.md)
