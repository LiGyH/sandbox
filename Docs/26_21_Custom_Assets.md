# 26.21 — Custom-ассеты и GameResource-расширения

## Что мы делаем?

В [00.29](00_29_GameResource.md) ты узнал про `GameResource` — настраиваемый ассет в виде C#-класса. Этот этап углубляет тему: как сделать **полностью свой формат файла**, как **расширить чужие** GameResource-классы дополнительными полями (плагины), как **сохранять бинарные данные** и как пользоваться **Cloud Assets** для контента из мастерской.

## Напоминание: что такое GameResource

```csharp
[GameResource( "Weapon Definition", "weapon", "Описание оружия" )]
public class WeaponResource : GameResource
{
    public string Title { get; set; }
    public int Damage { get; set; }
    public Model Model { get; set; }
    public SoundEvent ShootSound { get; set; }
}
```

Файл `*.weapon` появляется в редакторе, поля редактируются как у компонента. Это — текстовый JSON под капотом.

## Расширения GameResource (Resource Extensions)

Иногда **чужой** ресурс (от другого мода/библиотеки) нужно «обвесить своими полями», но не наследоваться. На помощь приходят **`GameResourceExtension`** (точное имя API смотри в актуальной документации):

```csharp
[GameResourceExtension( typeof( WeaponResource ) )]
public class WeaponSandboxData
{
    public bool ShowInSandboxMenu { get; set; }
    public string SandboxCategory { get; set; }
}
```

Теперь у любого `*.weapon`-файла появятся поля «Show in Sandbox Menu» и «Sandbox Category». Получить значения:

```csharp
var ext = weapon.GetExtension<WeaponSandboxData>();
if ( ext?.ShowInSandboxMenu == true ) { ... }
```

Полезно, когда **движок/библиотека вне твоего контроля**, но добавить «свои» метаданные надо.

## Custom-assets: свой формат файла

Если хочется не JSON, а **полностью свой формат** (например, бинарный массив высот, кастомный mesh), есть путь через `IAssetSource` / собственный импортёр. Шаги:

1. Зарегистрировать **расширение файла** (`.heightmap`).
2. Реализовать **импортёр**, читающий байты в `class HeightmapAsset : Asset`.
3. (Опционально) Написать **превью** — иконку в браузере ассетов.
4. (Опционально) Написать **редактор** — кастомное окно открытия.

Это серьёзная работа; берись только если стандартного `GameResource` не хватает (бинарные большие данные, своя нативная сериализация).

## Бинарная сериализация (BinarySerialization)

Для случаев, когда JSON тяжёл (длинные массивы, бинарный mesh), s&box предлагает свою **бинарную сериализацию**. Идея:

```csharp
[GameResource( "Heightmap Patch", "hpatch", "Кусок ландшафта" )]
public class HeightmapPatch : GameResource, ISerializeBinary
{
    public int Resolution;
    public float[] Heights;

    public void Read( BinaryReader br )
    {
        Resolution = br.ReadInt32();
        Heights = new float[Resolution * Resolution];
        for ( int i = 0; i < Heights.Length; i++ )
            Heights[i] = br.ReadSingle();
    }

    public void Write( BinaryWriter bw )
    {
        bw.Write( Resolution );
        foreach ( var h in Heights ) bw.Write( h );
    }
}
```

(Конкретные имена интерфейсов могут отличаться — проверяй актуальный `assets/resources/binary-serialization.md` Facepunch.)

Преимущество: 1024×1024 значений в JSON весят ~5 МБ и сохраняются медленно; в бинарнике — 4 МБ ровно и моментально.

## Cloud Assets

Когда ты ссылаешься в `GameResource` на ассет из **sbox.game** (например, модель из мастерской), движок **скачивает его на лету** при первом обращении. Это и есть **Cloud Assets**.

Полезные практики:

```csharp
[Property] public CloudAsset<Model> ExternalModel { get; set; }
```

Можно сохранить ссылку, скачать заранее (`await ExternalModel.LoadAsync()`) и показать индикатор загрузки. Это прячет «внезапный фриз» при первом спавне модели.

## Когда что выбрать

| Задача | Инструмент |
|---|---|
| Описать тип монстра/оружия | `GameResource` (JSON) |
| Расширить чужой `GameResource` своими полями | `GameResourceExtension` |
| Хранить очень большие массивы чисел | `GameResource` + `ISerializeBinary` |
| Свой формат файла для tooling | Custom asset + importer |
| Использовать ассет из мастерской | `CloudAsset<T>` |

## Подводные камни

- **Версионирование.** Если меняешь поля `GameResource`, не забывай про **миграции** старых файлов (часто достаточно дефолтных значений). См. *Component Versioning* в Facepunch-документации.
- **Бинарь не читаем глазами.** Это плюс к скорости и минус к диффам в Git. Не клади игровой баланс в бинарь — текст легче ревьюить.
- **Cloud-ассеты требуют интернет.** Сборка сервера должна это учитывать (или предзагружать).

## Что важно запомнить

- `GameResource` = типизированный JSON-ассет; основной инструмент для конфигов.
- `GameResourceExtension` = добавить свои поля в чужой ресурс, не наследуясь.
- `ISerializeBinary` (и подобные) = быстрая бинарная сериализация для тяжёлых данных.
- Свой формат файла — отдельный путь через importer, для серьёзных случаев.
- `CloudAsset<T>` подгружает ассет из мастерской по требованию; делай предзагрузку.
- Не забывай про миграции при изменении структуры ресурса.

## Что дальше?

В [26.22](26_22_Editor_Extensions.md) — расширения редактора: свои инструменты, окна, инспекторы.
