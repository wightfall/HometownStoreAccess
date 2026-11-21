# Hometown Store Access

An accessibility mod for **Hometown Store** (the Steam demo by Circle Cat). It lets you play the
whole game with a screen reader, the keyboard or a controller: menus, dialog, tooltips, the shop
floor, building, stocking shelves, customers, the town map, shops, the day summary and everything in
between are spoken. Sound cues help you find your way on the shop floor and the map.

Speech goes through [Prism](https://github.com/ethindp/prism), so it works with NVDA, JAWS, ZDSR and
other screen readers, and falls back to Windows speech (OneCore or SAPI) when no screen reader runs.

- Game version tested: Hometown Store Demo 0.7.11A (Steam app 4643920)
- Mod loader: [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23.6 (installed for you)
- Languages: English, Thai, Japanese, Indonesian and Traditional Chinese for both the mod and the
  game, and any other language through translation files ([how to translate](docs/TRANSLATING.md))

## Contents

- [Install](#install)
- [Getting started](#getting-started)
- [Keys](#keys)
- [Categories](#categories)
- [Shop floor scanner](#shop-floor-scanner)
- [Message history](#message-history)
- [Sound cues](#sound-cues)
- [Mod settings](#mod-settings)
- [Languages and translation](#languages-and-translation)
- [Troubleshooting](#troubleshooting)
- [Building from source](#building-from-source)
- [License](#license)

## Install

### With the installer (recommended)

1. Download `HometownStoreAccessInstaller.exe` from the
   [latest release](https://github.com/wightfall/HometownStoreAccess/releases/latest).
2. Close the game and run the installer. It finds the game through Steam. If it cannot, type or
   browse to the game folder (the folder with `HometownStore.exe`).
3. Choose the installer language in the first box. Two check boxes decide what happens after
   installing:
   - **Start the game after installing or updating**
   - **Start the game and the mod in the installer language**: sets the game's language and the
     mod's language to the language you chose for the installer.
4. Press **Install** (it says **Update** or **Reinstall** when the mod is already there).

The installer works offline, as the mod is built in. It installs BepInEx only when it is missing and
never touches other BepInEx mods. It needs no administrator rights, as Steam lets every user write to
its library. If a folder is protected anyway, it offers to try again as administrator.

**Updates:** when the installer starts, it asks GitHub whether a newer version exists. If one does, it
shows a window with what's new, one line per row in a read-only text box, and asks **Yes** or
**No**. Yes downloads the new installer and starts it. You can also press **Check for updates**.

**Uninstall:** press **Uninstall**. Your settings and your own translation files are kept for a later
reinstall. Tick **When uninstalling, also remove BepInEx** to remove BepInEx and everything in it as
well. The installer only offers this when no other mods need BepInEx.

Command line, for scripts:

```
HometownStoreAccessInstaller.exe [/install | /uninstall] [/removebepinex] [/launch] [/gamelang] [/quiet] [/lang:th] ["game folder"]
```

`/launch` starts the game afterwards. `/gamelang` makes the game and mod use the `/lang` language.
`/quiet` shows no message boxes. The exit code is 0 on success.

### By hand

1. Download `HometownStoreAccess-<version>.zip` from the release page.
2. Extract everything into the game folder, so that `winhttp.dll` sits next to `HometownStore.exe`.
   To find the folder in Steam: right-click Hometown Store Demo, then Manage, then Browse local files.
3. Start the game. The first start takes a little longer while BepInEx sets itself up.

## Getting started

Start the game from Steam. When the mod has loaded you hear "Hometown Store Access loaded. Press F1
for help." The mod skips the game's logo intro, so the main menu follows a moment later (turn this
off with **Skip the logo intro** in the settings).

- **F1** tells you the keys for the screen you are on.
- **Shift+F1** opens the key list: every key the mod uses, by section. It also has a section where
  you can listen to each sound cue. Page Up and Page Down jump between sections.
- **F5** opens the mod settings.

A new game starts with a letter: first choose whether you want the tutorial, then press Enter on
**Open letter**, read it with the arrow keys, and press Enter on the letter to go on. The game's own
tutorial runs on the first day. The mod adds a key hint to each tutorial step. Press **J** (Ctrl+J
in lists) to hear the current goal again.

## Keys

In lists and menus, typing a letter jumps to the next item that starts with it. That is why the
mod's letter commands need **Ctrl** while a list is open: for example Ctrl+I for details and Ctrl+R
to repeat. On the shop floor and in walk mode, letters work without Ctrl.

### Everywhere

| Key | Action |
| --- | --- |
| Up, Down | Move through items |
| Left, Right | Change a value (sliders, choices, amounts), or move |
| Enter | Activate |
| Shift+Enter | Alternative action (for example sell one, or move half a stack) |
| Ctrl+Enter | Third action (for example move one item) |
| Page Up, Page Down / Tab, Shift+Tab | Previous or next section (the main menu, the options and the mod settings have sections too) |
| Home, End | First or last item |
| Letters | Jump to the next item starting with that letter |
| Ctrl+Up, Ctrl+Down | Previous or next category, on screens with categories (see [Categories](#categories)) |
| Ctrl+I | Details about the focused item (tooltip, prices, contents) |
| Ctrl+R | Repeat the last message |
| Ctrl+Shift+R | Read the whole screen |
| F1 | Keys for this screen |
| Shift+F1 | Key list and sound cue preview |
| F5 | Mod settings |
| Escape | Back, close, or open the pause menu, as in the game. In a text box, Escape only stops editing. |

At the end of a list, Down goes round to the first item with the wrap sound, and Page Down in the
last section goes to the first section. To stop at the ends instead (with the edge sound), turn off
**Wrap around at the ends of lists** in the mod settings. This also applies to Left and Right on a
choice such as the game language; Enter always goes on to the next value.

### Main menu and options

The main menu has two sections: Continue, New game, Load game and Exit; then, with Page Down, the
game language, Options, Credits, Discord and the music switch. On Game language, Left and Right
switch the language at once and Enter goes on to the next one. Continue, New game and loading a save
say "Loading" as soon as you press Enter.

Two buttons act at once, without asking, and say so when you reach them: Exit closes the game, and
New game starts a new one whose autosave, from its second day, replaces the game that Continue loads.
To keep your current game, save it under its own name first (pause menu, Save).

Options (the pause menu in the game) has the sections General (full screen, show info, game
language), Sound (overall, music and effects volume), Game (save, back to the main menu, quit; only
in the game) and About (the version).

### Message history

| Key | Action |
| --- | --- |
| Comma, Period | Older, newer message in the current category |
| Shift+Comma, Shift+Period | First, last message |
| Left bracket, Right bracket | Previous, next category |
| Shift+Left bracket, Shift+Right bracket | First, last category |

These keys work while you play. The main menu, the pause menu, the save screen and the mod's own
screens (key list, settings) are not recorded, and there the keys do nothing.

### In the game

Hold Ctrl while a list is open.

| Key | Action |
| --- | --- |
| C | Money |
| T | Day, phase and time (travel points in the evening), money and shop level |
| P | Pause or resume while the shop is open |
| 1, 2, 3 | Normal, double, fast game speed |
| N | Next phase: open the shop, go to the map, or end the day |
| B | Build mode |
| K | Customers in the shop and what they are doing |
| J | Current goal or tutorial step |
| G | Everything in storage |

### Shop floor

| Key | Action |
| --- | --- |
| Left, Right | Walk one cell. A tick sounds for each step, lower in the back room. |
| Up, Down | Previous or next thing in the scanner category |
| Ctrl+Up, Ctrl+Down | Change the scanner category |
| Ctrl+Left, Ctrl+Right | Jump to the previous or next building |
| Home, End | The two ends of the shop |
| Enter | Open what is under the cursor: shelf, storage, machine, cashier, or the delivery truck at the far left |
| I | Full details of the cell |
| Tab | Switch to the toolbar (the game's on-screen buttons) |

### Build mode (B)

| Key | Action |
| --- | --- |
| Up, Down | Choose a building from your blueprints |
| Ctrl+Up, Ctrl+Down | Show one kind of building: shelves, storage, machines, cashiers, decorations |
| Enter | Start placing it |
| Shift+Enter | Sell one |
| Left, Right (while placing) | Move it. You hear whether it fits and what blocks it. |
| Enter, Escape (while placing) | Place it, or cancel |
| Tab | Switch between the blueprint list and the floor |
| Enter (on the floor) | Lift a building to move it |
| Delete (on the floor) | Put a building back into your blueprints |
| M (Ctrl+M in the list) | Toggle move mode |

### Shelves, storage, cashier and delivery truck

Each has two lists: the building's items and your storage (or the delivery truck). Tab jumps
between them. Enter moves the whole stack to the other side, Shift+Enter moves half and Ctrl+Enter
moves one. Ctrl+I describes an item: price, how it stacks, and which shelves it fits on. The
actions section has the move-everything buttons and Close. When the goods belong to more than one
category, Ctrl+Up and Ctrl+Down (or the Category item at the top) show one category at a time.

### Machines

Choose a recipe (the list opens on the recipe the machine uses now), set the daily amount with
Left and Right on Daily limit, and move ingredients from storage into the input. Finished goods
appear in the output. Move them to storage with Enter.

### Dialog

Lines are read automatically. Enter continues. Choose a reply with Up and Down, then press Enter;
each reply also says what it leads to when the game shows that. The Speed up and Dialog log
buttons are at the end of the list (Enter presses them). The log opens on the newest line; Up reads
the earlier ones, and Escape closes it.

| Key | Action |
| --- | --- |
| Backslash | The current line again |
| Shift+Backslash | The whole conversation so far, from its first line |

These two keys work only during a conversation. On a controller, hold the left trigger for the line.

### Town map (evening)

| Key | Action |
| --- | --- |
| Up, Down | Go through the places, with distance and compass direction from you |
| Enter | Details of a place, then its Travel button. Each trip costs one travel point. |
| Ctrl+W | Walk mode on or off |
| Arrows (walk mode) | Walk the roads step by step. Crossroads and places are announced and a beacon sounds when you arrive. |
| Enter (walk mode) | Visit the place you are standing on |
| I (walk mode) | Where you are and the nearest place |
| W (walk mode) | Leave walk mode |

Each place also says when it has an event from the event journal or is new, and a road sign says
when the area it leads to has such a place, like the game's markers on the map.

End of the day finishes the evening. The map's own buttons (customers, event journal, daily summary,
all stock, settings) are in the map section at the end of the list.

If you closed a shop with Escape, choose the place you are at again: its button says **Go in
again** and opens the shop without using a travel point. Your own shop is only a marker on the map;
you go back to it with End of the day.

### Shops on the map

The screen has these sections, and Page Up and Page Down move between them:

- **Filters**: the Category item and the search box. Ctrl+Up and Ctrl+Down (or Left and Right on
  Category) step through: all products, all goods, each kind of goods, all buildings, and each kind
  of building. The game's own tabs follow, so a sighted helper sees the same list.
- **Goods** and **Buildings**: everything this place sells, with price and stock ("sold out" when
  none is left). Right adds one to the basket, Left removes one, and holding Shift changes ten at
  a time. Ctrl+I describes an item and the profit you make on it.
- **Basket**: what you are buying (change amounts here too), the total and what money is left,
  then Check out and Close.

The goods arrive in your delivery truck the next morning.

### Controller

Xbox-compatible (XInput) controllers work directly. Other controllers work through Steam Input.

| Button | Action |
| --- | --- |
| D-pad, left stick | Arrow keys |
| A | Enter |
| X | Alternative action (Shift+Enter) |
| Y | Details |
| B | Back |
| LB, RB | Previous, next section |
| Start | Pause menu; hold for the mod settings (F5) |
| Back | Help for this screen (F1); hold for the key list (Shift+F1) |
| Left trigger | Repeat; in a conversation, hold to hear the line again (Backslash) |
| Right trigger | Status (day and time) |
| Left stick click | Customers |
| Right stick click | Pause or resume |
| Right stick up, down | Previous, next category (Ctrl+Up, Ctrl+Down) |
| Right stick left, right | Older, newer message (Comma, Period) |

## Categories

Screens with many kinds of things have categories. Ctrl+Up and Ctrl+Down change the category from
anywhere on the screen, and a **Category** item at the top of the list does the same with Left and
Right. The first choice always shows everything. After a change you hear the category and how many
entries it has, and the focus moves to the first one.

- **Shops on the map**: all products, goods, each kind of goods, buildings, each kind of building
- **Build mode**: shelves, storage, machines, cashiers, decorations
- **All stock** (G, or the toolbar and map button): each kind of goods
- **Shelves, storage and the delivery truck**: each kind of goods
- **Event journal**: events in progress and finished events

A screen opens again with everything shown. On the shop floor, Ctrl+Up and Ctrl+Down change the
scanner category instead.

## Shop floor scanner

Up and Down on the shop floor walk the cursor straight to the previous or next thing of the
current category and say "name, n of m". Ctrl+Up and Ctrl+Down change the category:

- **Everything**
- **Shelves**
- **Storage and machines**
- **Cashiers**
- **Decorations**
- **Customers**
- **Free spots**: empty stretches of shop floor or back room, with their width, so you know where
  a new building fits
- **Delivery truck**

## Message history

Everything the mod says is kept, and also sorted into categories: **all messages, customers,
sales, dialog, events, shop, tutorial and notices**. Comma and Period step through the current
category and Shift jumps to the first or last message. The bracket keys change the category.
Only messages from playing are kept: the menus, the pause menu, the save screen and the mod's own
screens are left out, and the history starts again when you go back to the main menu. During a
conversation, Backslash and Shift+Backslash repeat its lines directly (see [Dialog](#dialog)).

## Sound cues

You can listen to each cue in the key list (Shift+F1, then the sound cues section, then Enter).

| Cue | Meaning |
| --- | --- |
| Move | Moving in a list |
| Wrap | The list wrapped around, or a category changed |
| Edge | End of a list (when wrapping around is off) or of a value, or a wall |
| Activate | An item was activated |
| Error | Something did not work |
| Place OK | A building was placed |
| Place blocked | A building cannot go here |
| Cursor tick | One step of the shop floor cursor. The pitch is lower in the back room. |
| Building here | The cursor is on a building |
| Customer arrives | A customer walked in |
| Customer angry | A customer did not find what they wanted |
| Step | One step on the map in walk mode |
| Arrive | Arrived somewhere, or the shop closed |
| Beacon | Reached a place on the map |
| Open, Close | A panel opened or closed |

## Mod settings

F5 opens the settings. Changes are saved at once. Left and Right change a value, and Enter turns a
setting on or off. Page Up and Page Down jump between the sections.

- **Speech and language:** game language (every language the game has, plus your translation
  files), mod language, speech output (Auto, NVDA, JAWS, OneCore, SAPI and others), announce list
  positions, write speech to the log
- **Announcements:** customers coming and going, sales at the cashier, empty shelves, deliveries
  and machines, customer speech bubbles, every change of money, read dialog automatically,
  tutorial notes and key hints
- **Sound cues:** sound cues on or off, menu sounds, the overall sound cue volume
- **Volume of each sound cue:** a section of its own, so Page Down skips it
- **Controls:** wrap around at the ends of lists, skip the logo intro and shorten the loading fade,
  camera follows the shop floor cursor, controller support, controller rumble
- **Reset all settings to defaults:** press Enter twice

The settings are stored in `BepInEx\config\com.wightfall.hometownstoreaccess.cfg` in the game
folder, which you can also edit with a text editor while the game is closed.

## Languages and translation

The mod speaks the game's language. The game itself offers English and Thai in its options. The mod
makes all the game's languages available: English, Thai, Japanese, Indonesian and Traditional
Chinese. Choose them in the game language setting (F5, or the Options screen). The mod's own messages
are translated into the same five languages.

The game's Japanese, Indonesian and Chinese text is mostly untranslated in the demo. The mod ships
complete translations of the game text for these languages (story, items, places, customers and
menus), in `BepInEx\plugins\HometownStoreAccess\translations`. Text the game still lacks falls back
to English instead of showing an error. Some captions the game leaves in English in every language
("Next Day", "Quit To OS", the reward screen and others) are spoken in your language too.

Language names are also said in English ("ไทย, Thai", "日本語, Japanese"), so you always hear which
language you picked. With the mod language on Automatic, the mod speaks the game's language. If your
voice cannot read Japanese or Chinese letters, set the mod language to your own in F5: the game can
then show another language while the mod keeps speaking yours.

For any other language, you can translate both the mod and the game text with plain text files in
`BepInEx\plugins\HometownStoreAccess\lang`. Changes load while you play, without restarting. See
[docs/TRANSLATING.md](docs/TRANSLATING.md).

## Troubleshooting

- **No speech at all:** check that `winhttp.dll` and the `BepInEx` folder are in the game folder.
  Then look at `BepInEx\LogOutput.log`: it should contain "Hometown Store Access". If your screen
  reader is not picked up, set the speech output to it in the settings (F5).
- **Speech is too chatty:** turn off announcements you do not need in the settings, for example
  every change of money.
- **Something is not read or does not work:** press Ctrl+Shift+R to read the whole screen and
  F1 for the keys. Please report it in an
  [issue](https://github.com/wightfall/HometownStoreAccess/issues) with `BepInEx\LogOutput.log`
  attached. The log also contains everything that was spoken, which shows exactly where it went
  wrong.
- **The game opens with the wrong language:** set the game language in F5. Any
  `<code>.game.txt` file in the lang folder adds that language.
- **Developer cheats:** the game turns on hidden developer cheats when F1 is pressed three times
  within a second. Since F1 is the mod's help key, the mod stops those presses from counting and
  switches the cheats off when the game starts. Set `AllowDevCheats = true` in the `[Debug]`
  section of the settings file to keep them.
- **Escape in a text box:** Escape in a text box (shop search, save names) only stops editing;
  press it again to close the screen.
- **Everything went quiet or empty:** F3 is the game's own key for hiding its on-screen interface
  (for screenshots). The mod says when it is hidden; press F3 again to show it.
- **Saves:** saving from the pause menu (Save, then New save or an existing save) says "Saved as"
  with the name. Shift+Enter renames a save and Delete removes it, in the save and load lists.
- **The game feels slow:** the mod itself needs well under a millisecond per frame. If you turned
  off Skip the logo intro, the main menu appears only after the intro (about 15 seconds); any key
  skips it.

## Building from source

You need the .NET SDK (8 or later) and the game. The plugin compiles against the game's assemblies,
which are not part of this repository. Set `HOMETOWN_STORE_DIR` if the game is not in the default
Steam folder.

```
dotnet test tests\HometownStoreAccess.Tests\HometownStoreAccess.Tests.csproj
dotnet build src\HometownStoreAccess\HometownStoreAccess.csproj -c Release -p:DeployToGame=true
powershell -ExecutionPolicy Bypass -File tools\build-release.ps1
```

`DeployToGame` copies the plugin into the game. `build-release.ps1` runs the tests and writes the
release zip and the installer to `dist\`.

The code is organized like this:

- `src/HometownStoreAccess.Core`: engine-independent logic with unit tests: translations,
  navigation, text cleanup, the shop floor and map models, controller mapping, message history and
  translation files.
- `src/HometownStoreAccess`: the BepInEx plugin, with speech, screens, game hooks and audio.
- `src/HometownStoreAccess.Installer.Core` and `src/HometownStoreAccess.Installer`: the installer.
- `tests/HometownStoreAccess.Tests`: the unit tests.

## License

The mod is MIT licensed, see [LICENSE](LICENSE). It bundles Prism (MPL-2.0) and BepInEx (LGPL-2.1).
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Hometown Store is made by Circle Cat. This
is an unofficial fan mod and is not affiliated with the game's developers.
