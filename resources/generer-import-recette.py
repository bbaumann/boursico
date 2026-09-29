import csv, io, random, json, unicodedata, sys
random.seed(20260917)

root = sys.argv[1]
cities = json.load(io.open(f'{root}/src/AssociationSaleTool.Web/wwwroot/data/isere-postal-cities.json', encoding='utf-8'))
local = [('38120','Saint-Égrève')]*8 + [('38120','Fontanil-Cornillon')]*2 + [('38950','Saint-Martin-le-Vinoux')]*4 + \
        [('38340','Voreppe')]*4 + [('38360','Sassenage')]*3 + [('38600','Fontaine')]*3 + [('38000','Grenoble')]*5 + \
        [('38100','Grenoble')]*3 + [('38700','La Tronche')]*2 + [('38700','Corenc')] + [('38170','Seyssinet-Pariset')]*2 + \
        [('38180','Seyssins')]*2 + [('38240','Meylan')]*2 + [('38330','Saint-Ismier')] + [('38430','Moirans')]*2 + \
        [('38500','Voiron')]*2 + [('38640','Claix')] + [('38420','Domène')] + [('38610','Gières')] + [('38320','Eybens')]
for pc, c in set(local):
    assert c in cities[pc], (pc, c)

first_names = ["Michel","Nathalie","Pierre","Sophie","Jean","Isabelle","Philippe","Catherine","Alain","Sylvie","Christophe","Valérie",
 "Laurent","Sandrine","Nicolas","Céline","Julien","Aurélie","Sébastien","Émilie","Thomas","Camille","Antoine","Marie","François",
 "Hélène","Olivier","Christine","Vincent","Anne","Patrick","Stéphanie","Frédéric","Caroline","Guillaume","Élodie","Romain","Laure",
 "Mathieu","Delphine","David","Pauline","Bruno","Claire","Éric","Amandine","Jérôme","Lucie","Benoît","Agnès","Xavier","Chloé","Damien","Manon"]
last_names = ["Martin","Bernard","Dubois","Thomas","Robert","Richard","Petit","Durand","Leroy","Moreau","Simon","Laurent","Lefebvre",
 "Michel","Garcia","David","Bertrand","Roux","Vincent","Fournier","Morel","Girard","André","Lefèvre","Mercier","Dupont","Lambert",
 "Bonnet","François","Martinez","Legrand","Garnier","Faure","Rousseau","Blanc","Guérin","Muller","Henry","Roussel","Nicolas",
 "Perrin","Morin","Mathieu","Clément","Gauthier","Dumont","Lopez","Fontaine","Chevalier","Robin","Masson","Sanchez","Gérard","Nguyen"]
streets = ["rue de la Paix","avenue du Général de Gaulle","rue des Écoles","chemin des Vignes","allée des Tilleuls","rue Jean Jaurès",
 "avenue de Grenoble","rue du Moulin","impasse des Lilas","rue Victor Hugo","boulevard de la Chantourne","rue des Alpes",
 "chemin de la Combe","rue de Chartreuse","avenue de l'Europe","rue des Cerisiers","place de la Mairie","rue du Vercors",
 "allée du Néron","rue de la Gare","rue Pasteur","chemin du Rif","rue des Peupliers","avenue du Vercors","rue de Belledonne"]

catalog = [
 ("Lot de 3 livres Monsieur Madame",1,3),("Lot de 5 albums jeunesse",2,5),("Lot de livres Tchoupi",2,4),("Lot de BD Astérix",3,8),
 ("Lot de romans Bibliothèque Rose",2,5),("Livre pop-up Les animaux",2,4),("Lot de 4 livres Petit Ours Brun",2,4),
 ("Puzzle 100 pièces Pat'Patrouille",2,4),("Puzzle 500 pièces paysage",3,6),("Puzzle en bois ferme",2,5),("Puzzle 1000 pièces carte du monde",4,8),
 ("Lego City camion de pompiers",8,15),("Lego Friends café",6,12),("Lego Duplo train",8,15),("Lego Ninjago dragon",10,20),("Boîte de Lego en vrac 1 kg",8,15),
 ("Playmobil ferme",10,20),("Playmobil château de princesse",12,25),("Playmobil camion de police",6,12),("Playmobil pirates bateau",10,20),
 ("Poupée Corolle 36 cm",8,15),("Poupée Barbie sirène",3,6),("Maison de poupée en bois",15,30),("Poussette de poupée",5,10),("Lit de poupée",4,8),
 ("Peluche lapin",2,4),("Peluche ours 50 cm",3,6),("Doudou éléphant",1,3),("Peluche Simba",2,5),
 ("Jeu de société Monopoly Junior",4,8),("Jeu Dobble",3,5),("Jeu Uno",2,3),("Jeu Les Aventuriers du Rail",10,18),("Jeu Cluedo",5,8),
 ("Jeu Qui est-ce ?",4,6),("Jeu Puissance 4",3,5),("Jeu de 7 familles",1,2),("Jeu Mille Bornes",3,5),("Jeu Docteur Maboul",5,8),("Jeu Dixit",8,12),
 ("Voiture télécommandée",8,15),("Lot de 10 petites voitures",3,6),("Circuit de voitures Hot Wheels",8,15),("Camion benne Bruder",10,20),("Garage Vtech Tut Tut Bolides",8,15),
 ("Tracteur à pédales",20,30),("Trottinette 3 roues",10,20),("Vélo 16 pouces",25,30),("Draisienne en bois",15,25),("Porteur Vespa",15,25),
 ("Cuisine en bois",20,30),("Dînette en bois",5,10),("Marchande en bois",15,25),("Caisse enregistreuse",5,10),("Panier de fruits à découper",4,8),
 ("Déguisement princesse 4-6 ans",4,8),("Déguisement pompier 5-7 ans",4,8),("Déguisement Spiderman 6-8 ans",5,8),("Déguisement pirate",4,8),
 ("Tapis d'éveil",8,15),("Portique d'éveil",8,15),("Hochet et anneaux de dentition",2,4),("Cubes empilables",3,6),("Boîte à formes",3,6),
 ("Trieur de formes en bois",4,8),("Xylophone en bois",4,8),("Tambour et maracas",3,6),("Piano jouet",8,15),("Guitare enfant",8,15),
 ("Jeu de construction Kapla 200 planchettes",15,25),("Jeu de construction Magformers",15,25),("Mécano débutant",8,15),("Plaque de Lego verte",3,5),
 ("Ardoise magique",2,4),("Tableau double face",10,20),("Mallette de dessin",5,10),("Kit de perles Hama",4,8),("Coffret pâte à modeler",3,6),
 ("Coffret de peinture",4,8),("Tampons encreurs animaux",3,5),("Kit bracelets élastiques",3,6),("Métier à tisser enfant",4,8),
 ("Figurines Schleich chevaux lot de 6",8,15),("Figurines dinosaures lot de 8",4,8),("Château fort en bois",15,25),("Chevaliers Papo lot de 4",6,10),
 ("Toupie Beyblade",3,5),("Ballon de foot taille 4",3,6),("Raquettes de plage",2,4),("Jeu de quilles en bois",5,8),("Cerf-volant",3,6),
 ("Jeu de croquet",8,12),("Trampoline de poche",8,12),("Cible et fléchettes velcro",3,6),("Panier de basket enfant",8,15),
 ("Console Nintendo DS Lite avec 2 jeux",20,30),("Jeu Nintendo Switch Mario Kart",20,30),("Jeu PS4 FIFA 21",5,8),("Jeu Wii Sports",5,8),
 ("Talkie-walkie enfant",5,10),("Microscope enfant",8,15),("Coffret de chimie",8,15),("Globe terrestre lumineux",8,15),("Kit fouille dinosaure",4,8),
 ("Loupe et boîte à insectes",3,5),("Longue-vue enfant",4,8),("Lampe torche dynamo",3,5),("Appareil photo enfant Vtech",10,20),
 ("Train en bois Brio",15,25),("Circuit train électrique",15,30),("Locomotive Thomas et ses amis",4,8),("Rails en bois lot de 30",8,15),
 ("Garage Fisher-Price",8,15),("Ferme Fisher-Price avec animaux",8,15),("Arche de Noé en bois",8,15),("Pompe à essence Tut Tut",5,10),
 ("Pistolet Nerf",5,10),("Sabre laser lumineux",4,8),("Casque de chevalier et épée en bois",5,10),("Bouclier en bois",3,6),
 ("Mobile musical",5,10),("Veilleuse tortue",5,10),("Boîte à musique",4,8),("Tapis puzzle mousse",5,10),
 ("Luge en plastique",5,10),("Bateau de bain",2,4),("Jouets de bain lot de 6",2,4),("Livre de bain",1,2),
 ("Jeu Rush Hour",6,10),("Jeu Rubik's Cube",2,4),("Jeu Jenga",4,6),("Jeu Bazar Bizarre",5,8),("Jeu Loup-garou",4,6),("Jeu Time's Up Kids",6,10),
 ("Jeu Pique Plume",8,12),("Jeu Le Verger Haba",8,12),("Jeu Croque-Carotte",6,10),("Jeu Twister",4,8),
 ("Coffret Polly Pocket",4,8),("Coffret Sylvanian Families",8,15),("Coffret Pinypon",4,8),("Coffret LOL Surprise",3,6),
 ("Robot transformable",4,8),("Figurine Buzz l'Éclair",5,10),("Figurine Hulk 30 cm",5,10),("Voiture Cars Flash McQueen",3,6),
 ("Parking à étages",8,15),("Tapis circuit routier",4,8),("Brouette enfant",4,8),("Kit de jardinage enfant",4,8),
]
assert len(set(t for t,_,_ in catalog)) == len(catalog)

def make_email(fn, ln):
    s = unicodedata.normalize('NFD', f"{fn}.{ln}").encode('ascii','ignore').decode().lower().replace("'", "").replace(" ", "")
    return f"{s}@example.com"

def phone():
    return "0" + random.choice("67") + "".join(str(random.randint(0,9)) for _ in range(8))

def items(n):
    return [(t, random.randint(lo, hi)) for t, lo, hi in random.sample(catalog, n)]

def lines(rows):
    return "\n".join(f"{t}, {p}" for t, p in rows)

pairs = set()
while len(pairs) < 50:
    pairs.add((random.choice(first_names), random.choice(last_names)))
pairs = sorted(pairs)
random.shuffle(pairs)

counts = [2, 3, 50, 47, 45, 38, 33, 29, 25, 24] + [random.randint(4, 22) for _ in range(40)]
random.shuffle(counts)

rows = []
for i, (fn, ln) in enumerate(pairs, start=1):
    pc, city = random.choice(local)
    addr = f"{random.randint(1, 120)} {random.choice(streets)}"
    n = counts[i-1]
    rows.append([ln, fn, phone(), make_email(fn, ln), addr, pc, city, lines(items(n)), n])

error_notes = []
def set_err(idx, mutate, note):
    r = rows[idx]; mutate(r); error_notes.append(f"Ligne {idx+1} - {r[1]} {r[0]} : {note}")

def e1(r):
    r[4] = ""; r[5] = ""; r[6] = ""
set_err(6, e1, "adresse, code postal et ville vides (import OK, dépôt bloqué tant que l'adresse n'est pas complétée)")

def e2(r):
    r[4] = ""
set_err(14, e2, "adresse postale vide, code postal et ville présents")

def e3(r):
    base = r[7].split("\n")
    base[0] = base[0].rsplit(",",1)[0] + ", 7.50"
    base[1] = base[1].rsplit(",",1)[0] + ", 12,99"
    base.insert(2, "5")
    r[7] = "\n".join(base)
set_err(23, e3, "2 prix avec centimes (7.50 et 12,99) -> articles importés avec prix À compléter ; 1 ligne avec seulement un prix (5) -> ligne non importée")

def e4(r):
    base = r[7].split("\n")
    base[1] = base[1].rsplit(",",1)[0]
    base[3] = base[3].rsplit(",",1)[0] + ", gratuit"
    r[7] = "\n".join(base)
set_err(31, e4, "1 article sans prix, 1 article avec 'gratuit' à la place du prix -> 2 articles importés avec prix À compléter")

def e5(r):
    r[5] = "38120"; r[6] = "Grenoble"
    base = r[7].split("\n")
    base[-1] = base[-1].rsplit(",",1)[0]
    r[7] = "\n".join(base)
set_err(42, e5, "ville 'Grenoble' non reconnue pour le code postal 38120 -> ville vide + note Import à vérifier ; 1 article sans prix")

out = f'{root}/resources/recette-import-vendeurs-2026-09-17.csv'
with io.open(out, 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.writer(f, quoting=csv.QUOTE_ALL, lineterminator='\n')
    w.writerow(["Bourse aux jouets 2026 - recette"] + [""]*16)
    w.writerow(["Détails de la soumission"] + [""]*16)
    w.writerow(["Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"])
    for i, r in enumerate(rows, start=1):
        ln, fn, ph, em, addr, pc, city, art, n = r
        day = 1 + (i % 28); hh = 8 + (i % 12); mm = (i*7) % 60
        ts = f"{day:02d}/09/2026 - {hh:02d}:{mm:02d}"
        w.writerow([str(i), str(33800000+i), ts, ts, ts, "0", f"90.{random.randint(1,254)}.{random.randint(1,254)}.{random.randint(1,254)}", str(2790000+i), f"{fn.lower()}{i}", ln, fn, ph, em, addr, pc, city, art])

print("written", out, "sellers", len(rows), "item lines", sum(len(r[7].split(chr(10))) for r in rows))
print("counts min/max", min(counts), max(counts))
for e in error_notes: print(e)

# read back with the csv module to make sure the quoting round-trips
back = list(csv.reader(io.open(out, encoding='utf-8-sig', newline='')))
assert len(back) == 53, len(back)
assert back[2][9] == "Nom" and back[2][10] == "Prénom" and back[2][16] == "Jouet,Prix"
print("readback ok, data rows:", len(back) - 3)
