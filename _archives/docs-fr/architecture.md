# Architecture

## Architecture générale

L'architecture V1 recommandée est un petit système sur mesure sur réseau local :

- une machine serveur locale sur le réseau de l'événement
- une base de données centrale faisant autorité
- une UI dans le navigateur pour la saisie initiale et l'utilisation le jour de vente
- un outil ou script de rapprochement séparé pour le traitement post-vente

Il ne s'agit volontairement pas d'une architecture distribuée offline-first.

## Composants

### 1. Serveur central

Responsabilités :

- servir l'application web
- exposer la logique applicative
- se connecter à la base de données faisant autorité

Caractéristiques :

- situé sur le réseau local privé
- Internet est optionnel et n'est pas nécessaire pour faire fonctionner l'événement
- point unique de vérité numérique pendant l'événement

### 2. Base de données centrale

Responsabilités :

- stocker les `seller`, `item`, `sale` et leurs statuts
- stocker les seules données d'événement faisant autorité
- permettre un rapprochement post-vente déterministe

Caractéristiques :

- une seule base de données pour l'événement
- pas de base locale par poste en V1
- pas de client replay logs en V1

### 3. UI d'exploitation

Responsabilités :

- saisie des `item` avant l'événement
- recherche le jour de vente et création du panier
- enregistrement des ventes
- impression facultative de reçu

Caractéristiques :

- interface web
- UI large, simple et très contrastée
- optimisée pour des bénévoles âgés
- workflow très ciblé

### 4. Outil de rapprochement

Responsabilités :

- calculer les sorties par `seller` une fois les ventes terminées
- générer des fiches vendeur imprimables
- permettre de marquer des `item` comme `missing`
- régénérer des fiches vendeur corrigées

Caractéristiques :

- peut prendre la forme d'une UI séparée, d'une application console ou d'un script en V1
- n'a pas besoin de fonctionner pendant les ventes
- peut d'abord être utilisé par un personnel plus technique

## Flux de données

### Phase de saisie initiale

1. La liste des vendeurs est reçue en dehors du système.
2. Un organisateur crée manuellement les enregistrements `seller` et `item`.
3. Le système génère les références.
4. Les `item` sont prêts pour la recherche le jour de vente.

### Phase du jour de vente

1. L'opérateur saisit une partie d'une référence.
2. L'UI affiche les `item` correspondants.
3. L'opérateur sélectionne l'`item` voulu.
4. L'`item` est ajouté au panier en cours.
5. La `sale` est enregistrée.
6. Le statut de l'`item` devient indisponible pour toute vente ultérieure.

### Phase post-vente

1. L'outil de rapprochement lit les données de la base.
2. Les fiches vendeur sont générées.
3. Le stock physique est vérifié par rapport aux listes d'items retournés.
4. L'administrateur marque les `item` manquants si nécessaire.
5. Les fiches sont régénérées.
6. Le workflow final de paiement vendeur est exécuté.

## Principes architecturaux

- Garder le système centralisé pendant l'événement.
- Garder l'UI du jour de vente séparée des sujets de rapprochement.
- Préférer une solution de secours manuelle explicite à un comportement hors ligne partiel et caché.
- Optimiser pour la clarté opérationnelle plutôt que pour la sophistication technique.

## Direction technique suggérée

Aucun framework précis n'est encore figé, mais l'architecture suppose :

- une application web simple plutôt qu'un client desktop
- une UI server-rendered ou un frontend léger plutôt qu'une SPA lourde par défaut
- un backend appuyé sur une base de données avec des entités métier claires
- une implémentation du rapprochement séparée, pouvant évoluer indépendamment

Le framework exact et la base de données restent des choix d'implémentation ouverts.

## Pourquoi cette architecture a été choisie

- La partie difficile est le rapprochement, pas une fonctionnalité POS générique.
- L'UI du jour de vente doit être sur mesure et extrêmement simple.
- Construire une synchronisation locale par poste ajouterait trop de complexité pour la V1.
- Odoo résoudrait le mauvais 80 pour cent du problème pour ce périmètre.
