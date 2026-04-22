# Décisions

Ce fichier rassemble les principales décisions du projet dans un format ADR léger.

## D-001 Construire un outil sur mesure plutôt que centré sur Odoo

- Statut : acceptée
- Facteurs clés :
  - la partie difficile est le rapprochement de fin de journée, pas un comportement POS générique
  - l'UI du jour de vente doit être bien plus simple qu'une UI ERP/POS standard
  - Odoo introduirait un décalage conceptuel autour des produits, des vendeurs et du règlement
- Décision :
  - construire un petit outil événementiel sur mesure avec une base de données centrale et des flux conçus pour cet usage
- Conséquence :
  - davantage de travail d'implémentation sur mesure au départ
  - meilleure adéquation avec les opérations spécifiques à l'événement

## D-002 Découper le système en deux fonctionnalités indépendantes

- Statut : acceptée
- Facteurs clés :
  - les opérations du jour de vente et le rapprochement post-vente ont des utilisateurs différents et des besoins UX différents
  - le rapprochement n'a pas besoin de fonctionner pendant la vente
- Décision :
  - traiter les opérations de l'événement et le rapprochement post-vente comme deux fonctionnalités séparées au-dessus de la même base de données
- Conséquence :
  - chaque fonctionnalité peut être implémentée et évoluer de manière indépendante
  - inutile de construire une grosse application d'administration unifiée en V1

## D-003 Utiliser un seul serveur central et une seule base de données

- Statut : acceptée
- Facteurs clés :
  - besoin d'une source unique de vérité faisant autorité pendant l'événement
  - éviter la consolidation de fin de journée entre plusieurs bases par poste
  - garder l'architecture simple et auditable
- Décision :
  - déployer un serveur central et une base de données centrale sur le réseau local
- Conséquence :
  - une panne serveur arrête le workflow numérique
  - l'architecture reste bien plus simple qu'un système distribué ou offline-first

## D-004 Pas de synchronisation hors ligne ni de client replay logs en V1

- Statut : acceptée
- Facteurs clés :
  - la logique offline/replay créerait de la complexité de fusion, de duplication et de conflit
  - un arrêt net avec solution papier est acceptable sur le plan opérationnel
- Décision :
  - ne pas implémenter de logs locaux par poste ni de mécanisme de replay en V1
- Conséquence :
  - une indisponibilité du serveur impose de passer à des tickets papier
  - la V1 reste petite et prévisible sur le plan opérationnel

## D-005 La solution papier est la stratégie officielle en cas d'échec

- Statut : acceptée
- Facteurs clés :
  - les bénévoles ont besoin d'une procédure claire en mode dégradé
  - construire une continuité numérique coûterait trop cher pour la V1
- Décision :
  - si le serveur est indisponible, basculer sur des tickets papier numérotés puis effectuer un rapprochement manuel plus tard
- Conséquence :
  - la résilience dépend de la discipline opérationnelle plutôt que de la complexité logicielle

## D-006 Un jouet physique équivaut à un enregistrement `item`

- Statut : acceptée
- Facteurs clés :
  - chaque jouet a besoin de sa propre référence et de son propre statut
  - la logique de paiement et de retour fonctionne au niveau de l'item
- Décision :
  - modéliser chaque jouet physique comme un enregistrement `item` indépendant
- Conséquence :
  - la recherche, la vente et le rapprochement restent déterministes

## D-007 Le statut d'item est un seul champ à trois valeurs

- Statut : acceptée
- Facteurs clés :
  - le workflow a besoin d'un modèle de statut simple
  - une modélisation plus nuancée des états de vente et de rapprochement serait du sur-engineering pour la V1
- Décision :
  - utiliser un seul champ `status` avec les valeurs `available`, `sold`, `missing`
- Conséquence :
  - les items payables sont dérivés comme `sold + missing`
  - les items retournés sont dérivés comme `available`

## D-008 Le passage en caisse utilise une recherche par référence avec autocomplete

- Statut : acceptée
- Facteurs clés :
  - les bénévoles n'ont pas besoin du contexte vendeur
  - une saisie uniquement par code exact serait plus stressante et plus propice aux erreurs
  - le scan de code-barres n'est pas requis en V1
- Décision :
  - les opérateurs saisissent une référence, voient les résultats correspondants et sélectionnent explicitement l'item voulu
- Conséquence :
  - l'UI du jour de vente s'articule autour d'un grand composant de recherche autocomplete

## D-009 Ne pas afficher les informations vendeur pendant le passage en caisse

- Statut : acceptée
- Facteurs clés :
  - le contexte vendeur n'est pas pertinent pendant la vente
  - le passage en caisse doit rester simple et épuré
- Décision :
  - masquer les informations vendeur dans le flux du jour de vente
- Conséquence :
  - la logique vendeur reste limitée à la saisie initiale et au rapprochement

## D-010 Ne pas suivre les moyens de paiement en V1

- Statut : acceptée
- Facteurs clés :
  - d'un point de vue opérationnel, seul le fait qu'une vente soit "payée" compte
  - le détail du moyen de paiement n'apporte pas de valeur à l'événement en V1
- Décision :
  - suivre uniquement le fait qu'une `sale` a été complétée
- Conséquence :
  - flux de passage en caisse plus simple et modèle de données plus simple

## D-011 Les reçus sont optionnels et rares

- Statut : acceptée
- Facteurs clés :
  - la plupart des clients n'ont pas besoin de reçu
  - les reçus concernent principalement les cas de remboursement
- Décision :
  - l'impression de reçu est optionnelle et secondaire dans le flux de passage en caisse
- Conséquence :
  - la logique de reçu ne doit pas compliquer l'action principale d'enregistrement d'une vente

## D-012 La logique de frais vendeur reste hors du système en V1

- Statut : acceptée
- Facteurs clés :
  - le frais de 2 EUR par 15 items peut être géré hors système pour l'instant
  - la logique de relevé vendeur est plus simple si elle reflète uniquement les items payables et les items retournés
- Décision :
  - le montant final à payer au vendeur en V1 est uniquement la somme des `seller_price` des items payables
- Conséquence :
  - la gestion des frais vendeur est reportée sans bloquer le workflow principal

## D-013 La marge de l'association est stockée par item mais n'est pas utilisée pour le règlement vendeur

- Statut : acceptée
- Facteurs clés :
  - le prix de vente est le prix vendeur plus 20 pour cent
  - le règlement vendeur doit rester simple et basé uniquement sur `seller_price`
- Décision :
  - stocker sur chaque `item` à la fois le prix vendeur et le prix de vente visible côté client
- Conséquence :
  - le règlement vendeur reste simple
  - la marge de l'association peut être analysée séparément

## D-014 Utiliser des références d'item centrées sur le vendeur

- Statut : acceptée
- Facteurs clés :
  - le tri physique post-vente repose sur le regroupement des items d'un même vendeur
  - 26 compartiments à une seule lettre sont insuffisants pour 100 à 200 vendeurs
  - l'ancienne astuce des numéros réservés devient inutile avec une numérotation par vendeur
- Décision :
  - utiliser des références au format `SELLERCODE-ITEMNO`, par exemple `BK-014`
- Conséquence :
  - les références regroupent naturellement les items par vendeur
  - les items ajoutés tardivement prennent simplement le numéro suivant chez le vendeur

## D-015 Les codes vendeur utilisent un alphabet sécurisé sur 2 lettres

- Statut : acceptée
- Facteurs clés :
  - les références doivent être faciles à lire et à écrire à la main
  - les lettres ambiguës augmentent le risque d'erreur opérationnelle
- Décision :
  - exclure `I`, `O`, `Q` et `V` de la génération des `seller_code`
- Conséquence :
  - l'espace de codes vendeur reste suffisamment grand pour l'événement
  - la confusion côté opérateur est réduite

## D-016 Les codes vendeur sont attribués dans un ordre réparti

- Statut : acceptée
- Facteurs clés :
  - les vendeurs doivent être répartis sur les premières lettres pour faciliter le tri physique post-vente
- Décision :
  - attribuer les codes vendeur selon un ordre par colonnes, par exemple `AA`, `BA`, `CA`, ..., puis `AB`, `BB`, `CB`, ...
- Conséquence :
  - la distribution des codes est visuellement et opérationnellement équilibrée

## D-017 Les numéros d'item sont des séquences à largeur fixe par vendeur

- Statut : acceptée
- Facteurs clés :
  - une numérotation à largeur fixe améliore la lisibilité et le tri
- Décision :
  - utiliser des numéros d'item par vendeur tels que `001`, `002`, `003`
- Conséquence :
  - les références se trient de façon prévisible
  - les items ajoutés tardivement restent simples à attribuer
