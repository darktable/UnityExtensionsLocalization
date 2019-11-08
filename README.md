# UnityExtensionsLocalization
This is a Unity localization system that uses excel files during development.

## Features
- You can use multiple excel files, each containing multiple sheets, and each sheet containing only parts of contents of parts of   languages. As the localization system works, it reads all excel files in the Localization directory (include sub-directories) and merges their contents, which means the file organization structure can be customized.

- You can define keywords and then use "{ }" to reference them later.
<p align="center">
  <img src="Documentation~/KeywordReference.png"><br>
   <em>Keyword Reference</em>
</p>

- You can make texts as atrributes. Marking a TextName with an "@" tells the localization system that it is a language attribute. The difference between normal texts and attributes is that attributes can always be accessed without loading the specific language pack. The "LanguageName" attribute is indispensable, which is the localized name of the language. The localization system sorts languages by LanguageName by default, so you can easily create and display a user-friendly list of language choices.
<p align="center">
  <img src="Documentation~/LanguageAttribute.png"><br>
   <em>Language Attribute</em>
</p>

- Can use the following escape characters: "\n"-line break, "\t"-tab, "\\"-backslash, "{{"-a single "{", "}}"-a single "}".
<p align="center">
  <img src="Documentation~/EscapeCharacter.png"><br>
   <em>Escape Character</em>
</p>

- And, you can preview localized contents in edit-mode.
<p align="center">
  <img src="Documentation~/LocalizationWindow.png"><br>
   <em>Localization Window</em>
</p>

## Installation
You need to set your project's Api Compatibility Level to .NET 4.x first (menu: Edit > Project Settings > Player), then open the package manager window (menu: Window > Package Manager), select "Add package from git URL...", fill in this in the pop-up textbox: https://github.com/yuyang9119/UnityExtensionsLocalization.git.

## Quick Start
1. Create a "Localization" folder in your project root directory, it is the same directory as the "Assets".
2. Create and edit excel files, save them in the Localization folder ([Download Sample.xlsx](Documentation~/Sample.xlsx)).
3. Open the Localizaton window in unity (menu: Window > Localization), click "Build Packs".
4. Call LocalizationManager.LoadMetaAsync when your game starts running.
5. Call LocalizationManager.LoadLanguageAsync to load a language.
6. After loading a language, you can call LocalizationManager.GetText to get a localized text.
7. If you want to change language at runtime, just call LocalizationManager.LoadLanguageAsync again.
