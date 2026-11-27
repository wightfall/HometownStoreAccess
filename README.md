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
- [Steam achievements](#steam-achievements)
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
well. The installer only offers this when no other mods need BepInEx. If another program has one of
the files open (a virus scanner, or an editor with a log open), everything else is still removed and
the installer says so: close that program and press Uninstall again.

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
- **Shift+F1** opens the key list: every key the mod uses, by section, one line at a time. It also
  has a section where you can listen to each sound cue. Each section starts with a heading of its
  own (for example "Actions, heading, 2 items"): Page Up and Page Down jump to the headings, and
  Down reads the first line of the section.
- **F5** opens the mod settings.
- **With a controller**, Back tells you the buttons for the screen you are on, holding Back opens
  the key list, and holding Start opens the mod settings. See [Controller](#controller).

A new game starts with a letter: first choose whether you want the tutorial, then press Enter on
**Open letter**, read it with the arrow keys, and press Enter on the letter to go on. The game's own
tutorial runs on the first day. The mod adds a key hint to each tutorial step, naming the controller's buttons
while you play with a controller. Press **J** (Ctrl+J in lists), or hold the right trigger, to hear
the current goal again.

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
| Page Up, Page Down / Tab, Shift+Tab | Heading of the previous or next section |
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

A list with more than one section starts each section with a heading line of its own, such as
"Basket, heading, 3 items": the main menu, the options, the map, the shops, shelves, the credits, the
key list and the mod settings all have them. Page Up and Page Down (or Tab) land on the heading, so
you hear the section's name alone, and Down goes on to its items, which are read without the section
name in front of them. A list with only one section has no heading: its name is said with the item
you reach first. Enter on a heading does nothing.

At the end of a list, Down goes round to the first item with the wrap sound, and Page Down in the
last section goes to the first section. To stop at the ends instead (with the edge sound), turn off
**Wrap around at the ends of lists** in the mod settings. This also applies to Left and Right on a
choice such as the game language or a category, to Ctrl+Up and Ctrl+Down on categories, and to Up
and Down in the shop floor scanner; Enter always goes on to the next value.

### Main menu and options

The main menu has two sections: Continue, New game, Load game and Exit; then, with Page Down, the
heading "Settings and more", followed by the game language, Options, Credits, Discord and the music
switch. On Game language, Left and Right
switch the language at once and Enter goes on to the next one. Continue, New game and loading a save
say "Loading" as soon as you press Enter.

Two buttons act at once, without asking, and say so when you reach them: Exit closes the game, and
New game starts a new one whose autosave, from its second day, replaces the game that Continue loads.
To keep your current game, save it under its own name first (pause menu, Save).

Credits are in sections, as the game shows them: Developed by, Core team, Collaborators, Testers
and Special thanks. Page Down goes from one heading to the next.

Options (the pause menu in the game) has the sections General (full screen, show info, game
language), Sound (overall, music and effects volume), Game (save, back to the main menu, quit; only
in the game) and About (the version).

When you save under a name that is already taken (an empty name means "Save Game"), the game warns
that the save will be overwritten, and you hear the warning. Pressing Confirm again overwrites that
save; Cancel keeps it. Changing the name, or opening the name box again, starts over, so the game
always warns before it overwrites a save. (In the game itself the warning stays armed after Cancel,
and the next Confirm overwrites without one; the mod fixes that.)

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
| T | Day, phase and time (travel points in the evening), money, shop level and weather |
| P | Pause or resume while the shop is open |
| 1, 2, 3 | Normal, double, fast game speed |
| N | Next phase: open the shop, go to the map, or end the day |
| B | Build mode; B again closes it |
| K | Customers in the shop and what they are doing |
| J | Current goal or tutorial step |
| G | Everything in storage |

Each morning the game picks the day's weather: clear, rain or fog, light, moderate or heavy. It is
only a picture on the screen, so the mod says when it starts, changes or stops ("Weather: heavy
rain"), and T says it at any time. A save that loads with rain or fog says it too; clear weather
is said only when it follows rain or fog. To keep quiet about it, turn off **Weather** in the mod
settings (T still says it). The game calls its weather "still in testing" in version 1.7.12.

Many events in the demo have no name in any language (the game's event journal shows an empty
title). The mod names them after what they ask for: the place they send you to ("Event at
Temple"), else the goods or buildings they need ("Event: Paper"), else the shop level. The game
strikes a task through when it is done; the mod says "done" after it, in J and in the event
journal. When every task is done the event ends quietly, as in the game: what it leads to may come
later (another day, or a customer in the shop).

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

Walking onto a shelf reads every stack on it (a wooden shelf has 8 slots, so up to 8 kinds of
goods), and walking onto storage says how full it is and what is in it. All storage buildings
share one storage, so each one reads the same goods. The delivery truck also lists everything it
carries.

When the truck comes with goods you hear "Delivery truck has arrived" and what it brings. It comes
every morning, and also whenever it holds goods and the map is closed: goods you buy on the map in
the evening are in the truck in front of your shop as soon as you close the map, as the game shows
it (the game's tutorial says they come the next morning, but the game itself sends the truck at
once).

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
| B (Ctrl+B in the list) | Close build mode (the Close item in the tools section does the same) |

### Shelves, storage, cashier and delivery truck

Each has two lists: the building's items and your storage (or the delivery truck). Tab jumps
between them. Enter moves the whole stack to the other side, Shift+Enter moves half and Ctrl+Enter
moves one. Ctrl+I describes an item: price, how it stacks, and which shelves it fits on. The
actions section has the move-everything buttons, which say what they moved ("Moved to the shelf:
Candy: 22"), and Close. When the goods belong to more than one category, Ctrl+Up and Ctrl+Down (or
the Category item at the top) show one category at a time.

On a shelf opened with the storage or the truck, each slot is followed by its lock, a check box
("Lock slot to Candy, not checked"), as the game shows a lock button on each slot. Enter locks the
slot to its kind of goods: it stays on the shelf when it sells out or you take the goods away
("Candy: 0"), and moving everything from the storage fills it with the same goods again. Unlocking
an empty slot removes it.

Enter on a cashier opens its looks. The game shows them only as pictures, so the mod names them:
plain ones in your language (Default, Cat, Frozen), the others by their own names.

I on the shop floor, and Ctrl+I in build mode, describe a building like the game's tooltip: name,
description, size, value, and what a shelf holds.

The character collection says each character's number, so the ones not met yet ("Character 3: not
met yet") can be told apart. Game bug: in the demo the collection's entries have no pictures, so the
game never marked anyone as met and the book stayed empty for everyone. The mod marks a character as
met when a conversation shows them, as the game meant, and the save keeps it. A character the game
has no name for is named the way the conversations call them.

### Machines

Choose a recipe (the list opens on the recipe the machine uses now), set the daily amount with
Left and Right on Daily limit, and move ingredients from storage into the input. Finished goods
appear in the output. Move them to storage with Enter.

### Dialog

Lines are read automatically. Enter continues. Choose a reply with Up and Down, then press Enter;
each reply also says what it leads to when the game shows that. The Speed up and Dialog log
buttons are at the end of the list (Enter presses them). The log opens on the newest line; Up reads
the earlier ones, and Escape closes it.

A reward after a conversation is read as soon as it appears. If you press Enter before that, Enter
reads it, and the next Enter closes it.

The pictures the game shows without words are said before the line: who appears while someone
else is speaking (the narrator, say), who leaves, a character's new face (the college student smiling or
surprised, the otaku after his makeover and back), a picture that shakes (the game shakes it on a
strong reaction) and a story picture. When someone else is
speaking, they come with the character's name. The demo has no story pictures, but later versions
might. Turn these off with **Faces and story pictures in conversations** and **Shaking pictures in
conversations** in the settings.

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
when the area it leads to has such a place, like the game's markers on the map. A place's details
also name its events ("Events here: Event at Temple").

End of the day finishes the evening. The map section at the end of the list has your travel points,
your money (the map shows it since game 1.7.12), what you bought today, End of the day, Close map and
the map's own buttons (customers, event journal, daily summary, settings, all stock).

After a trip the focus is on the place you arrived at ("Temple, you are here"), also when you
close a shop or come into another area by a road sign. To go back into a shop, the lottery or the
vehicle shop after closing it (with its Close item or Escape), press Enter on the place again: its
button says **Go in again** and opens it without using a travel point, even with no points left.
This is how the game works at every place: going back into the place you are at is free, only
moving to another place costs a point.

Some places have no screen at all (the temple, the school, the village head's house, the farm and
others in the demo). The game opens nothing there, so you only hear that you arrived, and their
button is the game's own "Travel: 0", which opens nothing either. An event can still ask you to go
there: arriving is what completes that task.

A shop, the lottery and the vehicle shop always open at the top of their list, and so do shelves,
machines and the cashier's looks; only coming back from a popup inside them keeps your place.
When a place's details are open, the map's last item says **Close the place's details**; after that
it closes the map. Your own shop is only a marker on the map; you go back to it with End of the
day.

### Shops on the map

The screen has these sections, and Page Up and Page Down move between them:

- **Filters**: the Category item and the search box. Ctrl+Up and Ctrl+Down (or Left and Right on
  Category) step through: all products, all goods, each kind of goods, all buildings, and each kind
  of building. The game's own tabs follow, so a sighted helper sees the same list.
- **Goods** and **Buildings**: everything this place sells today, with price and stock ("sold out" when
  none is left). Right adds one to the basket, Left removes one, and holding Shift changes ten at
  a time. Ctrl+I describes an item: what customers pay and your profit, the shelves that hold it,
  and which of your own shelves (placed or not yet placed) do, as the game's list shows them. For a
  building it gives what the game's tooltip shows: size, value, slots for goods and what it holds.
- **Basket**: what you are buying (change amounts here too), the total and what money is left,
  then Check out and Close.

The goods go into your delivery truck at once. When you close the map the truck is in front of your
shop with them, also in the evening, and you hear it arrive (see [Shop floor](#shop-floor)).

What a shop sells is chosen by the game: it changes from day to day (some goods are picked at
random, and the amounts too), and more goods appear as your shop level goes up. The mod lists
everything the shop has that day, even what the game's own list hides behind its tabs and filters.

### Controller

Xbox-compatible (XInput) controllers work directly. Other controllers work through Steam Input.

The mod says when you plug a controller in or out. While you play with the controller, what the mod
tells you names its buttons instead of keys: the tutorial hints, the help for each screen (Back),
the hints on items, and the prompts for placing a building and for starting the day. The key list
(hold Back) then starts with the controller section. When you press a key on the keyboard, the mod
names the keyboard keys again, until you use the controller again. Ctrl, Shift, Alt, Insert, Caps
Lock and the number pad do not count, so your screen reader's keys (such as Ctrl to stop speech)
leave it as it is.

| Button | Action |
| --- | --- |
| D-pad, left stick | Arrow keys |
| A | Enter |
| X | Alternative action (Shift+Enter); hold for the third action (Ctrl+Enter): move one item, delete a save |
| Y | Details; hold to read the whole screen (Ctrl+Shift+R) |
| B | Back |
| LB, RB | Previous, next section; hold for the first, last item (Home, End) |
| Start | Pause menu; hold for the mod settings (F5) |
| Back | Help for this screen (F1); hold for the key list (Shift+F1) |
| Left trigger | Repeat; in a conversation, hold to hear the line again (Backslash) |
| Right trigger | Status (day and time); hold for the current goal or tutorial step (J) |
| Left stick click | Customers; on the map, walk mode (Ctrl+W) |
| Right stick click | Pause or resume; hold for the next category of the message history (Right bracket) |
| Right stick up, down | Previous, next category (Ctrl+Up, Ctrl+Down) |
| Right stick left, right | Older, newer message (Comma, Period) |

On the shop floor, RB (like Tab) switches to the toolbar, the game's own buttons: the next phase
(open the shop, go to the map), build mode, the game speed, all stock, customers, the event journal
and the others. That is how you open build mode and move the day on with a controller. Only typing (a save's
name), the first and last message of the history and F3 need the keyboard.

The buttons with a second action on hold (X, Y, LB, RB and the right stick click) act when you let
go of them. Holding X a little too long on something without a third action plays the edge sound
instead of doing what A does.

## Categories

Screens with many kinds of things have categories. Ctrl+Up and Ctrl+Down change the category from
anywhere on the screen, and a **Category** item at the top of the list does the same with Left and
Right. The first choice always shows everything. After a change you hear the category and how many
entries it has, and the focus moves to the first one.

- **Shops on the map**: all products, goods, each kind of goods, buildings, each kind of building
- **Build mode**: shelves, storage, machines, cashiers, decorations
- **All stock** (the toolbar and map button; G only reads the storage aloud): each kind of goods
  (the list starts with the game's own lines: the total, the storage slots in use, and the goods
  waiting on the delivery truck; each product then says how many are in storage, on shelves and,
  when there are any, waiting for delivery)
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

## Steam achievements

Steam shows an unlocked achievement only in its overlay, which screen readers cannot read. When the
game unlocks one, the mod says "Achievement unlocked:" with its name and description as Steam gives
them. To turn this off, use **Steam achievements** in the mod settings.

## Mod settings

F5 opens the settings. Changes are saved at once. Left and Right change a value, and Enter turns a
setting on or off. Each section starts with a heading of its own: Page Up and Page Down jump to the
headings, so you hear the section name alone, and Down goes to its first setting.

- **Speech and language:** game language (every language the game has, plus your translation
  files), mod language, speech output (Auto, NVDA, JAWS, OneCore, SAPI and others), announce list
  positions, write speech to the log, a detailed log for bug reports
- **Announcements:** customers coming in and getting upset, customers leaving the shop (customers
  who come in or leave together are said in one message), sales at the cashier, empty shelves,
  deliveries and machines, customer speech bubbles, every change of money (a sale, a purchase, a
  lottery ticket or a building you sell is said once, with the money you have now), read dialog
  automatically, faces and story pictures in conversations, shaking pictures in conversations,
  tutorial notes and key hints, Steam achievements, weather (rain and fog)
- **Sound cues:** sound cues on or off, menu sounds, the overall sound cue volume
- **Volume of each sound cue:** a section of its own, so Page Down skips it
- **Controls:** wrap around at the ends of lists, skip the logo intro and shorten the loading fade,
  camera follows the shop floor cursor, controller support, controller rumble
- **Reset all settings to defaults:** press Enter twice. A game language chosen in the mod goes back
  to the game's own choice as well.

The settings are stored in `BepInEx\config\com.wightfall.hometownstoreaccess.cfg` in the game
folder, which you can also edit with a text editor while the game is closed.

## Languages and translation

The mod speaks the game's language. The game itself offers English and Thai in its options. The mod
makes all the game's languages available: English, Thai, Japanese, Indonesian and Traditional
Chinese. Choose them in the game language setting (F5, or the Options screen). The mod's own messages
are translated into the same five languages. With the mouse, the game's own language list (English and
Thai only) works too: what you pick there replaces a language chosen in the mod. A language chosen in
the mod also becomes the game's own choice when it is English or Thai.

The game's Japanese, Indonesian and Chinese text is mostly untranslated in the demo. The mod ships
complete translations of the game text for these languages (story, items, places, customers and
menus, including the new lines of game version 1.7.12), in
`BepInEx\plugins\HometownStoreAccess\translations`. Text the game still lacks falls back
to English instead of showing an error. Some captions the game leaves in English in every language
("Next Day", "Quit To OS", the reward screen and others) are spoken in your language too. A few
spelling mistakes in the game's Thai text that make a voice misread them are corrected (for example
the detergent, "ผงซักฟอก", or "ปฏิเสธ" for Decline); the game shows the corrected text too.

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
  reader is not picked up, set the speech output to it in the settings (F5). When the output you
  choose cannot start (it is not installed or not running), the mod says so and uses the best one
  available.
- **Speech is too chatty:** turn off announcements you do not need in the settings, for example
  every change of money.
- **Something is not read or does not work:** press Ctrl+Shift+R to read the whole screen and
  F1 for the keys. Please report it in an
  [issue](https://github.com/wightfall/HometownStoreAccess/issues) with `BepInEx\LogOutput.log`
  attached. The log also contains everything that was spoken, which shows exactly where it went
  wrong. **Detailed log for bug reports** in the settings adds more detail to it.
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
- **The game feels slow:** the mod itself needs well under a millisecond per frame, and it checks
  the lang folder and looks for a controller on a background thread, so neither ever holds up a
  frame. If you turned off Skip the logo intro, the main menu appears only after the intro (about
  15 seconds); any key skips it.

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

### Test commands

The mod has commands for testing it in the running game. They change the game like cheats do, so
use them only on a test save:

- **Back up your saves first.** The game always autosaves to AutoSave, the save that Continue
  loads, even when you loaded another slot. Copy the folder
  `%USERPROFILE%\AppData\LocalLow\Circle Cat\Hometown Store\saves` before testing and put it
  back afterwards (Continue loads the save with the newest file time, so keep the times).
- Load your test save with **Load game**, never with Continue.

To turn them on, close the game and set `TestHooks = true` in the `[Debug]` section of
`BepInEx\config\com.wightfall.hometownstoreaccess.cfg` (`Verbose = true` adds detail to the log).
Then every line written to `BepInEx\hsa_cmd.txt` in the game folder runs as a command: the mod
reads the file five times a second, runs each line and deletes the file. For example, in a command
prompt in the game folder:

```
echo state> BepInEx\hsa_cmd.txt
```

The results, and everything the mod says (lines with `[Speech]`), are written to
`BepInEx\LogOutput.log`. The game only runs while its window has the focus, so a command waits
until you switch back to the game. Write the next command only after the file is gone (writing it
again before then replaces the commands not yet read). Several lines in one file run together: key
presses with the same Shift, Ctrl and Alt arrive in the same frame, and another combination or the
same key again in the next frame. Lines that start with `#` are skipped.

Names are the game's English asset names, and part of a name is enough, in any case (`Incense`
finds "Incense Sticks"). `buildings`, `trades`, `places` and `journals` list them. When you are
done, set `TestHooks = false` again and delete `BepInEx\hsa_dumps` and any `BepInEx\hsa_*` files.

**The mod itself**

| Command | What it does |
| --- | --- |
| `help` | Writes the list of commands to the log. |
| `state` | The active mod screen, how many items its list has, and what is focused. |
| `say <text>` | Speaks the text. |
| `key <KeyCode> [shift] [ctrl] [alt]` | Presses a mod key, with Unity key names such as `DownArrow`, `Return`, `PageDown`, `F1` or `B`. Only the mod sees it: keys the game reads itself (Escape, the speed keys 1 to 3, typing into a text field) need a real key press. |
| `pad <buttons> [frames]` | Controller input, held for that many frames (2 if not given; holding about 0.6 s, as for the key list, needs about 240). Buttons: `a b x y start back lb rb ls rs up down left right lt rt`, sticks: `lsup lsdown lsleft lsright rsup rsdown rsleft rsright`. |
| `pad connect`, `pad disconnect` | Does what plugging a controller in or out does: says so, and makes the instructions name the controller's buttons, or the keyboard keys again. |
| `achievements` | The game's Steam achievements: whether Steam is connected, whether each one is unlocked, what the mod says when it unlocks, and whether the guard below is on. |
| `achievement <name or number>` | Says that achievement's announcement, to hear how it is read. It unlocks nothing. |
| `achievement lock <name or number>` | Locks that achievement again on Steam, to undo a test that let the game unlock it. |
| `cues`, `cues play` | Every sound cue: its length, its loudest point and its volume setting. `cues play` also says each one's name and plays it. |
| `click <path>` | Presses the button at that path in the scene (the paths are in a dump). |
| `pictures [conversation]` | Lists the conversations' story pictures, every portrait that changes from one line to the next, how often the pictures fade, dim or shake, and the portraits' entries in the game's collection. With a conversation name (or part of it): each of its lines with its speaker and its pictures. |
| `pick <dropdown> <item>` | Opens the shown dropdown with that object name (as in a dump, for example `Language`) and clicks its item (counted from 0), as the mouse does. |
| `dump [name]` | Writes the screen's interface tree to `BepInEx\hsa_dumps\ui_<name>_<time>.txt`. |
| `world` | Writes the shop grid, the buildings and the customers to `BepInEx\hsa_dumps\world_<time>.txt`. |
| `perf [seconds]` | Measures for 5 seconds (or as many as given) how long each part of the mod takes per frame, and logs `[perf]` lines. |

**Money, goods and buildings**

| Command | What it does |
| --- | --- |
| `cash <n>` | Adds money; a minus sign takes it away. |
| `item <name> [n]` | Puts goods into the storage. |
| `stock <item> [n]` | Puts goods straight onto the first placed shelf that can hold them. When no placed shelf can, the log names the shelves that can. Events that ask for goods on shelves count these. |
| `blueprint <name> [n]` | Adds buildings to your blueprints, to place in build mode. |
| `buildings` | Lists every building with its kind and category. |

**Shops and the map**

| Command | What it does |
| --- | --- |
| `trades` | Every shop, how many goods and buildings it sells today, and whether it is unlocked. |
| `trade <shop>` | Opens that shop's screen without travelling. |
| `places` | Every area of the map with its places and road signs: name, unlocked or not, today's stock, lottery or vehicle shop, where you are, the events waiting there, and the places the map hides. Open the map once first. |
| `unlock <place>`, `unlock all` | Makes that place, or every place, available on the map, as a reward does. |
| `points <n>` | Sets today's travel points. |
| `goto <place>` | Does what arriving at the place does: uses a travel point if you move, checks the events, and opens the shop, lottery or vehicle screen. Your token on the map does not move. |
| `open lottery`, `open vehicle`, `open characters` | Opens the lottery, the vehicle shop or the character collection. |

**Events, story and rewards**

| Command | What it does |
| --- | --- |
| `journals` | Every event of the event journal: its name (or the name the mod gives one without a name), whether it is not started, in progress or finished, hidden or repeating, and its tasks. |
| `journal <name>` | Starts that event: it is added to the event journal. |
| `journal check` | Checks the tasks of every event now, as the game does when you arrive at a place. |
| `events` | Lists every story event the game's own developer tool can run. |
| `event <name>` | Runs that story event now. |
| `dialog`, `dialog <name>` | Lists the conversations that have replies; shows that conversation. |
| `reward money <n>`, `reward item <name>`, `reward blueprint <name>`, `reward customer <name>`, `reward trust <customer>`, `reward budget <customer>`, `reward wish <customer>`, `reward trade <place>`, `reward vehicle <Motorcycle or Car>` | Shows that kind of reward screen, to hear how it is read. It only shows the screen and gives nothing. |

**Game text**

| Command | What it does |
| --- | --- |
| `table <table> [entry]` | How each language resolves a game text table, and one entry of it. |
| `export <locale>` | Writes the game's own texts in that language (`th`, `ja`, `zh-HK`, `id`, `en`) to `BepInEx\hsa_export_<locale>.txt`. |
| `missing <locale>` | Writes the game texts that language lacks to `BepInEx\hsa_missing_<locale>.txt`, as a template for a translation file (see `docs/TRANSLATING.md`). |

**How to test with them**

- **A full shelf:** `stock Candy 5`, `stock Bread 5` and so on put each kind of goods on the first
  placed shelf that holds it (for another shelf: `blueprint Wooden Shelf` and place it in build
  mode first). Walking onto the shelf reads every stack.
- **Every place on the map:** in the evening, `unlock all`, `points 40` and `places`. Then go to
  each place with Up, Down and Enter (or `goto <place>`), and close each shop with Escape. After
  each trip the focus is on the place you arrived at.
- **An event:** `journals` shows them all. `journal Shaman` starts the temple event and you hear
  "New event: Event at Temple"; J reads its tasks. For its goods: `blueprint Altar`, place the
  altar shelf, `stock Incense`, `stock holy water` and `stock Sacred Thread`. Then travel to the
  temple (or `goto Temple`): every task is done and the event ends.
- **Rewards and conversations:** `reward trade VillageHead`, `reward item Candy`; `dialog` lists
  the conversations with replies, `dialog <name>` plays one.
- **The controller:** `pad a`, `pad back 240` (holds Back: the key list), `pad rt 240` (holds the
  right trigger: the current goal). After a `pad` command the instructions name the controller's
  buttons (F1 or `pad back` tells), and a real key press brings back the keyboard keys.
  `pad connect` and `pad disconnect` say what plugging a controller in and out says.
- **Achievements:** `achievements` lists them; `achievement Money1000` says how "Money 1,000" is
  announced. No test command unlocks an achievement. A test can still make the game unlock one
  itself (a conversation, money, a reward), so while the test commands are on, the game's own
  unlocks are not sent to Steam: they are written to the log instead.
- **Sound cues:** `cues play` plays every cue after its name. Each should be clearly audible, also on
  laptop speakers.
- **A translation:** `missing ja` writes what the Japanese game text lacks.

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
