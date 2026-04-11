# 22_12 — Расширение субтитров (SubtitleExtension)

## Что мы делаем?

Создаём расширение ресурса (Resource Extension), которое добавляет текст субтитров к звуковым файлам. Когда NPC произносит реплику, система речи автоматически ищет привязанные субтитры и отображает их на экране.

Это элегантный подход: вместо хранения текста отдельно от звука, субтитры «прикрепляются» непосредственно к звуковому файлу как метаданные.

## Как это работает внутри движка

- `ResourceExtension<TResource, TSelf>` — механизм s&box для расширения существующих типов ресурсов дополнительными данными. В данном случае мы расширяем `SoundFile`.
- Атрибут `[AssetType]` регистрирует расширение в редакторе ассетов:
  - `Name = "Subtitle"` — отображаемое имя.
  - `Category = "Sounds"` — категория в редакторе.
  - `Extension = "ssub"` — расширение файла (`.ssub`).
- Когда `SpeechLayer` воспроизводит звук, он вызывает `ResourceExtension<SoundFile, SubtitleExtension>.Get(soundFile)` для получения текста субтитров.
- Атрибут `[TextArea]` на свойстве `Text` говорит инспектору отображать многострочное текстовое поле вместо однострочного.

## Путь к файлу

```
Code/Npcs/Speech/SubtitleExtension.cs
```

## Полный код

### `SubtitleExtension.cs`

```csharp
namespace Sandbox.Npcs;

/// <summary>
/// Extends a SoundFile to add subtitle text that will be shown when the sound is played. This is used for Npc speech.
/// </summary>
[AssetType( Name = "Subtitle", Category = "Sounds", Extension = "ssub" )]
public partial class SubtitleExtension : ResourceExtension<SoundFile, SubtitleExtension>
{
	/// <summary>
	/// The text to show in the subtitle for this sound event. If empty, no subtitle will be shown.
	/// </summary>
	[Property, TextArea] public string Text { get; set; }
}
```

## Разбор кода

### Объявление класса

```csharp
public partial class SubtitleExtension : ResourceExtension<SoundFile, SubtitleExtension>
```

- `partial` — позволяет движку генерировать дополнительный код (сериализация, инспектор и т.д.).
- `ResourceExtension<SoundFile, SubtitleExtension>` — generic-базовый класс:
  - Первый параметр (`SoundFile`) — тип ресурса, который мы расширяем.
  - Второй параметр (`SubtitleExtension`) — сам тип расширения (паттерн CRTP — Curiously Recurring Template Pattern).

### Атрибут `[AssetType]`

```csharp
[AssetType( Name = "Subtitle", Category = "Sounds", Extension = "ssub" )]
```

| Параметр | Значение | Описание |
|---|---|---|
| `Name` | `"Subtitle"` | Имя типа ассета в редакторе |
| `Category` | `"Sounds"` | В какой категории отображается |
| `Extension` | `"ssub"` | Расширение файла на диске |

Это означает, что для каждого звукового файла (например, `npc_greeting.sound`) можно создать файл `npc_greeting.ssub`, который будет автоматически привязан к нему.

### Свойство `Text`

```csharp
[Property, TextArea] public string Text { get; set; }
```

- `[Property]` — свойство отображается в инспекторе и сериализуется.
- `[TextArea]` — отображается как многострочное текстовое поле (удобно для длинных реплик).
- Если `Text` пустой или `null` — субтитры не показываются.

### Как это используется в слое речи

Хотя это не входит в данный файл, вот как `SpeechLayer` читает субтитры:

```csharp
// Примерная логика внутри SpeechLayer
var subtitle = SubtitleExtension.Get( soundFile );
if ( subtitle != null && !string.IsNullOrEmpty( subtitle.Text ) )
{
    ShowSubtitle( subtitle.Text );
}
```

Метод `Get()` наследуется от `ResourceExtension` и возвращает экземпляр расширения для указанного ресурса (или `null`, если расширение не создано).

### Рабочий процесс создания реплики

1. Создайте звуковой файл NPC-реплики (`npc_hello.sound`).
2. В редакторе ассетов создайте файл субтитров (`npc_hello.ssub`).
3. В инспекторе введите текст: *«Привет! Добро пожаловать в наш город.»*
4. В расписании NPC добавьте задачу `Say(soundEvent)`.
5. При воспроизведении звука субтитры появятся автоматически.

## Что проверить

1. Создайте `.ssub` файл для звукового ассета — он должен появиться в редакторе в категории «Sounds».
2. Заполните поле `Text` — должно отображаться как многострочное поле ввода.
3. Запустите NPC с задачей `Say(soundEvent)` — субтитры должны появиться на экране.
4. Оставьте `Text` пустым — субтитры не должны отображаться.
5. Проверьте, что файл `.ssub` корректно связывается с соответствующим `.sound` файлом.
