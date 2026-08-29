# Installation — Outil de bourse aux jouets

Application web autonome (ASP.NET Core + SQLite). Elle contient son propre
serveur HTTP et son propre moteur .NET : **aucun runtime .NET n'est à installer**.

- Écoute sur le port **5050**
- Une seule base de données, dans le fichier `data/bourse-aux-jouets.db`
- Prévue pour être lancée à la main le jour de l'événement, puis arrêtée

---

## 1. Prérequis

### Debian

**Bibliothèque ICU** — .NET en a besoin pour la gestion des langues et des
formats de date. Elle est présente sur la plupart des installations Debian,
mais pas sur une installation minimale.

```sh
sudo apt update
sudo apt install libicu76
```

Si ce paquet n'existe pas sous ce nom (le numéro change selon la version de
Debian), cherchez-le avec `apt-cache search --names-only '^libicu[0-9]'` et
installez celui qui apparaît.

**Pare-feu** — le port 5050 doit être ouvert pour que les autres postes du
réseau local puissent accéder à l'application.

```sh
sudo ufw allow 5050/tcp
```

(Adaptez si vous utilisez `nftables` ou `iptables` directement.)

### Windows 10

Aucun prérequis. Avant de décompresser l'archive, faites un clic droit dessus
→ **Propriétés** → cochez **Débloquer**, sinon Windows bloquera l'exécutable.

---

## 2. Décompression

### Debian

```sh
sudo mkdir -p /opt/bourse
sudo unzip bourse-aux-jouets-linux-x64.zip -d /opt/bourse
cd /opt/bourse
chmod +x demarrer.sh AssociationSaleTool.Web
```

> **Le `chmod +x` est obligatoire.** L'archive est fabriquée sous Windows, qui
> ne conserve pas le bit d'exécution Unix. Sans cette commande vous obtiendrez
> `Permission denied` au démarrage.

### Windows 10

Décompressez `bourse-aux-jouets-win-x64.zip` dans un dossier de votre choix,
par exemple `C:\bourse`.

---

## 3. Démarrage

### Debian

```sh
cd /opt/bourse
./demarrer.sh
```

### Windows 10

Double-cliquez sur `demarrer.bat`.

### Vérifier que le démarrage a réussi

Au premier lancement, l'application crée le dossier `data/` et sa base de
données. Cela prend quelques secondes. **Attendez ces deux lignes :**

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5050
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

L'application est alors prête. Testez-la sur la machine elle-même :
<http://localhost:5050>

**Pour arrêter l'application : `Ctrl+C` dans la fenêtre.** L'arrêt est propre,
aucune donnée n'est perdue.

> **Important si vous démarrez via SSH :** l'application s'arrête dès que vous
> fermez la session SSH. Lancez-la depuis une console sur la machine elle-même,
> ou dans un `tmux` / `screen` que vous pourrez détacher.

---

## 4. Accès depuis le réseau local

Relevez l'adresse IP du serveur :

```sh
ip -4 addr show | grep inet
```

Les autres postes accèdent alors à l'application par :

```
http://<adresse-ip-du-serveur>:5050
```

Exemple : `http://192.168.1.42:5050`

Il n'y a rien à installer sur les postes clients : un navigateur suffit.

> L'application est en **HTTP simple**, sans chiffrement, et sans compte
> utilisateur. Elle est prévue pour un réseau local fermé et ne doit pas être
> exposée sur Internet.

---

## 5. Sauvegarde des données — le plus important

Toutes les données (vendeurs, articles, ventes) sont dans **un seul dossier** :
`data/`. Rien d'autre n'est à sauvegarder.

Ce dossier contient jusqu'à trois fichiers, qui doivent **toujours être copiés
ensemble** :

```
data/bourse-aux-jouets.db
data/bourse-aux-jouets.db-wal
data/bourse-aux-jouets.db-shm
```

> Copier uniquement le `.db` sans le `-wal` peut faire perdre les dernières
> ventes enregistrées.

### Sauvegarde propre (recommandée)

Arrêtez l'application (`Ctrl+C`), puis copiez le dossier :

```sh
cp -a /opt/bourse/data /media/usb/sauvegarde-bourse-$(date +%Y%m%d-%H%M)
```

Vous pouvez ensuite relancer l'application.

### Sauvegarde pendant l'événement

Sans arrêter l'application, la même commande reste acceptable à condition de
copier les trois fichiers en une seule opération (`cp -a` sur le dossier
entier le fait). **Faites-le plusieurs fois dans la journée**, par exemple
toutes les heures.

### Restauration

Arrêtez l'application, remplacez le dossier `data/` par la copie, redémarrez.

---

## 6. Mise à jour vers une nouvelle version

**Ne décompressez jamais la nouvelle archive par-dessus l'ancien dossier** :
vous risqueriez d'écraser la base de données.

```sh
cd /opt
sudo unzip bourse-aux-jouets-linux-x64.zip -d /opt/bourse-nouveau
sudo cp -a /opt/bourse/data /opt/bourse-nouveau/data
sudo chmod +x /opt/bourse-nouveau/demarrer.sh /opt/bourse-nouveau/AssociationSaleTool.Web
sudo mv /opt/bourse /opt/bourse-ancien
sudo mv /opt/bourse-nouveau /opt/bourse
```

L'ancien dossier est conservé sous `/opt/bourse-ancien` : gardez-le jusqu'à
confirmation que la nouvelle version fonctionne. Les éventuelles évolutions
de la base de données sont appliquées automatiquement au premier démarrage.

---

## 7. Dépannage

| Message / symptôme | Cause | Solution |
|---|---|---|
| `Permission denied` au lancement | Bit d'exécution absent | `chmod +x demarrer.sh AssociationSaleTool.Web` |
| `Couldn't find a valid ICU package installed on the system` | Bibliothèque ICU manquante | Installer `libicu` (étape 1) |
| `Address already in use` / `Failed to bind to address` | Le port 5050 est déjà pris, souvent par une instance déjà lancée | Fermer l'autre fenêtre, ou `ss -lptn 'sport = :5050'` pour identifier le processus |
| Fonctionne en `localhost` mais pas depuis un autre poste | Pare-feu, ou mauvaise adresse IP | Ouvrir le port 5050 (étape 1), vérifier l'IP (étape 4) |
| L'application s'arrête toute seule | Session SSH fermée | Lancer depuis une console locale ou dans `tmux` |
| La base semble vide au démarrage | L'application a été lancée depuis un autre dossier et a créé une base neuve | Toujours lancer via `demarrer.sh`, qui se place dans le bon dossier |
| Page d'erreur générique dans le navigateur | Erreur applicative | Relever les lignes `fail:` / `error:` dans la fenêtre de démarrage et les transmettre au développeur |

En cas de problème, la fenêtre de démarrage contient l'intégralité du journal :
c'est le premier élément à transmettre.
