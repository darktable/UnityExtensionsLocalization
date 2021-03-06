# UnityExtensionsLocalization
This is a localization system for Unity that uses excel in development.

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

- Use “^” as TextName to inherit the prefix of the previous TextName and automatically increments the number of the suffix. This is automatic numbering function. This requires that the first of a series of automatic numbering must explicitly specify the prefix and initial suffix number. The advantage of using this function is that you don't have to manually modify TextNames to ensure that the numbers are continuous when inserting a line, deleting a line, or adjusting line orders. Your scripts can also access these lines through a loop.
<p align="center">
  <img src="Documentation~/AutomaticNumbering.png"><br>
   <em>Automatic Numbering</em>
</p>

- You can preview localized contents in edit-mode.
<p align="center">
  <img src="Documentation~/LocalizationWindow.png"><br>
   <em>Localization Window</em>
</p>

## Note
This package does not contain UI support directly, if you are looking for a complete localization solution please use this: https://assetstore.unity.com/packages/slug/158921 (It supports UnityUI & TextMeshPro).

## Installation
Unity can not install dependent packages automatically at present, you have to install them manually:
- https://github.com/90-games/UnityExtensionsCommon
- https://github.com/90-games/UnityExtensionsExcelDataReader

Open the package manager window (menu: Window > Package Manager), select "Add package from git URL...", fill in this in the pop-up textbox: https://github.com/90-games/UnityExtensionsLocalization.git.

## Quick Start
1. Create a "Localization" folder in your project root directory, it is the same directory as the "Assets".
2. Create and edit excel files, save them in the Localization folder ([Download Sample.xlsx](Editor/EditorResources/Sample.xlsx)).
3. Open the Localizaton window in unity (menu: Window > Localization), click "Build Packs".
4. Call LocalizationManager.LoadMetaAsync when your game starts running.
5. Call LocalizationManager.LoadLanguageAsync to load a language.
6. After loading a language, you can call LocalizationManager.GetText to get a localized text.
7. If you want to change language at runtime, just call LocalizationManager.LoadLanguageAsync again.
