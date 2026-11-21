# Translating Hometown Store Access

You can translate the mod's messages and the game's own text into any language, including
languages the game does not have. You only need a text editor such as Notepad.

All translation files live in the mod's `lang` folder:

```
<game folder>\BepInEx\plugins\HometownStoreAccess\lang
```

The mod writes two templates there the first time the game runs, and updates them when a new
version adds text:

- `template.mod.txt` has every message of the mod (about 700).
- `template.game.txt` has every text of the game (about 3200), such as menus, items, characters
  and dialog.

## Start a translation

1. Copy a template and name the copy after your language code, for example `es.mod.txt` and
   `es.game.txt` for Spanish, or `pt-BR.mod.txt` for Brazilian Portuguese. You can make only one
   of the two files if you like.
2. Open the copy. Each entry shows the English text in a comment, followed by the key:

   ```
   # Hometown Store Access loaded. Press F1 for help.
   mod_loaded =
   ```

   Write your translation after the `=`:

   ```
   mod_loaded = Hometown Store Access cargado. Pulsa F1 para obtener ayuda.
   ```

3. Save the file as UTF-8 (Notepad does this by default).
4. In the game, press F5 and choose your language under game language. It appears there as soon
   as the file exists. The mod's own language follows the game language when the mod language
   setting is Auto.

Saved changes load while you play, usually within two seconds, and the mod says "Translations
reloaded." You do not need to restart the game.

## Rules

- One entry per line: `key = text`. Do not change the keys.
- Lines that start with `#` are comments.
- An entry with no text keeps the original. You can translate a little at a time.
- `\n` is a line break.
- A key that ends in `.pad` is the controller version of the key before it (for example
  `hint_open_building.pad`). The mod uses it instead while the player uses a controller, so it names
  the controller's buttons. Keep the button names printed on the controller (A, B, X, Y, LB, RB,
  Back, Start) as they are.
- Keep `{0}`, `{1}` and so on. The mod fills in names, numbers and amounts there, and you can move
  them to wherever your language needs them.
- In game text, keep tags in angle brackets such as `<b>` or `<color=#ff0000>` and placeholders in
  curly braces exactly as they are.

## Settings at the top of a file

```
@name = Español
@base = en
```

- `@name` is the name of the language as shown in the settings.
- `@base` (game text only) is the game language shown for anything you have not translated: `en`,
  `th`, `ja`, `id` or `zh-HK`. The default is English.

## Improving a built-in language

The mod ships with English, Thai, Japanese, Indonesian and Traditional Chinese. A file named after
one of these codes (`th.mod.txt`, `ja.game.txt`, ...) replaces just the entries it contains. That
way you can fix a single message without touching the rest.

The game's demo leaves most of its Japanese, Indonesian and Chinese text untranslated, so the mod
also ships game text translations for them in the mod's `translations` folder (next to `lang`):
`ja.game.txt`, `id.game.txt`, `zh.game.txt` and `th.game.txt`. They hold only what the game itself
lacks. Do not edit those files, as updates replace them. Your own `lang\ja.game.txt` (or `zh-HK`,
`id`, `th`) loads on top of them, and its entries win. In the source code they are in the
`translations` folder; corrections there are welcome as pull requests.

A few captions the game leaves in English in every language ("Next Day", "Quit To OS", the
reward screen and others) are mod messages whose keys start with `fixed_` and `reward_`.

## Sharing your translation

Translations are welcome in the project. Open an issue or a pull request at
<https://github.com/wightfall/HometownStoreAccess> and attach your `.mod.txt` file, your
`.game.txt` file or both. Updating or uninstalling the mod keeps your files.
