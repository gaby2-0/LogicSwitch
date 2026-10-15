# SignalForge — StellarDrive

Par **leterrible20**.

*[English below](#english)*

Ajoute à StellarDrive des pièces d'électronique pour le réseau de signaux : lampes, portes logiques, boutons, interrupteur, sectionneur électrique et porte pilotable. Toutes les pièces sont dans l'onglet **SignalForge** du menu de construction, sans recherche à débloquer, et une page **SignalForge** du guide (touche G) explique chaque pièce.

## Pièces

| Pièce | Fonctionnement |
|---|---|
| **Lampe à écran** | Hublot qui s'allume selon le signal, écran où l'on écrit son texte avec **F**. Pas besoin de courant. |
| **Hublot lumineux** | Le hublot seul, qui s'allume selon le signal branché sur son côté. Pas besoin de courant. |
| **Porte logique** | **F** change de fonction : AND, OR, XOR, NAND, NOR, XNOR, NOT, SR (mémoire), T (bascule), BUF. |
| **Porte ET / OU / XOR** | Une seule fonction, affichée sur l'écran. |
| **Bouton poussoir NO** | Envoie 1 quand on appuie avec **F** (au moins 0,4 s, et tant que **F** reste enfoncé). |
| **Bouton poussoir NF** | Vrai contact, dans les deux sens : au repos le signal passe d'une prise à l'autre, coupé (0) pendant l'appui (au moins 0,4 s, et tant que **F** reste enfoncé). |
| **Interrupteur** | **F** bascule entre 0 et 1. |
| **Sectionneur** | Laisse passer le courant entre ses deux prises électriques (jusqu'à 100 kW) quand son signal vaut 0,5 ou plus, le coupe sinon. Signal sur l'un ou l'autre côté ; sans câble de signal, le courant passe. Poignée et voyant indiquent l'état. |
| **Porte à signal** | S'ouvre quand le signal de son cadre vaut 0,5 ou plus, se ferme en dessous. Sans câble, porte manuelle. |

**Couleur des lampes :** signal sous 0,25 rose, sous 0,5 vert, sous 0,75 jaune, au-dessus rouge ; à 0, éteinte.

**Logique :** un signal de 0,5 ou plus compte comme 1. La sortie d'une porte vaut 1 ou 0.

## Installation

1. Avoir [MelonLoader](https://melonwiki.xyz/) 0.7.3 et le mod **SDModKit** installés (ils le sont si vous utilisez StellarModManager avec d'autres mods).
2. Copier `SignalForge.dll` dans le dossier `Mods` du jeu.
3. Si vous aviez une ancienne version appelée **ElectroLogic**, supprimez `Mods\ElectroLogic.dll` (vos pièces déjà posées sont conservées).

En multijoueur, tous les joueurs doivent avoir le mod.

## Bon à savoir

- Le texte de la lampe à écran sert aussi de canal de Signal Transmitter : un Signal Receiver réglé sur le même nom reçoit la valeur de la lampe. Évitez de reprendre le nom d'un canal déjà utilisé.
- Le mod utilise les identifiants de pièces 4801 à 4813.
- Les noms et descriptions s'affichent en français si le jeu est en français, sinon en anglais (pris en compte au lancement).

## Compiler depuis les sources

Le script utilise le compilateur C# fourni avec Windows ; il suffit que le jeu soit installé (chemin à adapter en haut de `build.ps1`).

```
powershell -ExecutionPolicy Bypass -File build.ps1            # compile dans bin\
powershell -ExecutionPolicy Bypass -File build.ps1 -Install   # compile et copie dans Mods
powershell -ExecutionPolicy Bypass -File build.ps1 -Package   # compile et crée dist\SignalForge-<version>.zip
```

---

<a name="english"></a>
# SignalForge — StellarDrive (English)

By **leterrible20**.

Adds signal electronics to StellarDrive: lamps, logic gates, push buttons, a toggle switch, a circuit breaker and a remote-controlled door. Every part is in the **SignalForge** tab of the build menu, with no research to unlock, and a **SignalForge** page in the guide (G key) explains every part.

## Parts

| Part | What it does |
|---|---|
| **Screen Lamp** | Window that lights up from the signal, screen you write your own text on with **F**. No power needed. |
| **Light Porthole** | Just the window, lit by the signal plugged into its side. No power needed. |
| **Logic Gate** | **F** cycles the function: AND, OR, XOR, NAND, NOR, XNOR, NOT, SR (latch), T (toggle), BUF. |
| **AND / OR / XOR Gate** | A single function, shown on the screen. |
| **Push Button NO** | Sends 1 when pressed with **F** (at least 0.4 s, and as long as **F** is held). |
| **Push Button NC** | A real contact, both ways: at rest the signal passes from one plug to the other, cut (0) while pressed (at least 0.4 s, and as long as **F** is held). |
| **Toggle Switch** | **F** flips between 0 and 1. |
| **Circuit Breaker** | Passes power between its two power plugs (up to 100 kW) while its signal is 0.5 or more, cuts it otherwise. Signal on either side; with no signal cable, power flows. Handle and light show the state. |
| **Signal Door** | Opens while the signal on its frame is 0.5 or more, closes below. With no cable it is a manual door. |

**Lamp colours:** signal below 0.25 pink, below 0.5 green, below 0.75 yellow, above that red; at 0 it is off.

**Logic:** a signal of 0.5 or more counts as 1. A gate outputs 1 or 0.

## Install

1. Have [MelonLoader](https://melonwiki.xyz/) 0.7.3 and the **SDModKit** mod installed (they are if you already use StellarModManager with other mods).
2. Copy `SignalForge.dll` into the game's `Mods` folder.
3. If you had an older version called **ElectroLogic**, delete `Mods\ElectroLogic.dll` (parts you already placed are kept).

In multiplayer every player needs the mod.

## Good to know

- The Screen Lamp's text is also a Signal Transmitter channel: a Signal Receiver set to the same name gets the lamp's value. Avoid reusing a channel name you already use.
- The mod uses part ids 4801 to 4813.
- Names and descriptions are in French when the game is in French, in English otherwise (read at launch).

## Build from source

The script uses the C# compiler that ships with Windows; only the game needs to be installed (adjust the path at the top of `build.ps1`).

```
powershell -ExecutionPolicy Bypass -File build.ps1            # build into bin\
powershell -ExecutionPolicy Bypass -File build.ps1 -Install   # build and copy into Mods
powershell -ExecutionPolicy Bypass -File build.ps1 -Package   # build and create dist\SignalForge-<version>.zip
```
