# 01.11 — Запись демо (DemoRecording)

## Что мы делаем?

Этот файл настраивает **параметры записи видео (демо)** в игре. Когда игрок записывает геймплей через встроенную систему MovieMaker, этот код исключает из записи:

1. **Вьюмодели** (модели оружия от первого лица) — помеченные тегом `"viewmodel"`.
2. **Объекты поверхностей** — префабы из папки `"prefabs/surface/"`.

Зачем? Вьюмодели предназначены только для вида от первого лица и не нужны при свободной камере. Поверхности (декали, следы) — это временные эффекты, которые загромождают запись.

## Как это работает внутри движка?

### `MovieMaker`

`Sandbox.MovieMaker` — встроенная система s&box для записи видео из игры. Она захватывает сцену покадрово и создаёт видеофайл.

### `[DefaultMovieRecorderOptions]`

Этот атрибут говорит движку: «Используй этот метод для настройки параметров записи по умолчанию». Движок вызывает `BuildMovieRecorderOptions` автоматически при старте записи.

### Fluent API (цепочка вызовов)

```csharp
options.WithFilter(...).WithFilter(...)
```
Паттерн «строитель» (Builder): каждый `.WithFilter()` добавляет фильтр и возвращает тот же объект, позволяя цепочку.

## Создай файл

Путь: `Code/Utility/DemoRecording.cs`

```csharp
using Sandbox.MovieMaker;

namespace Sandbox;

public static class DemoRecording
{
	[DefaultMovieRecorderOptions]
	public static MovieRecorderOptions BuildMovieRecorderOptions( MovieRecorderOptions options )
	{
		return options
			.WithFilter( x => !x.Tags.Has( "viewmodel" ) )
			.WithFilter( x => x.PrefabInstanceSource?.StartsWith( "prefabs/surface/" ) is not true );
	}
}
```

## Разбор кода

### Подключение пространства имён

```csharp
using Sandbox.MovieMaker;
```
Подключаем систему записи видео. Это **не** глобальный using — нужно только в этом файле.

### Атрибут `[DefaultMovieRecorderOptions]`

```csharp
[DefaultMovieRecorderOptions]
public static MovieRecorderOptions BuildMovieRecorderOptions( MovieRecorderOptions options )
```
- `[DefaultMovieRecorderOptions]` — маркер для движка: «вызови этот метод при записи».
- Метод получает текущие настройки `options` и возвращает модифицированные.

### Первый фильтр — исключение вьюмоделей

```csharp
.WithFilter( x => !x.Tags.Has( "viewmodel" ) )
```
- `x` — каждый объект на сцене.
- `x.Tags.Has("viewmodel")` — проверяет, есть ли тег `"viewmodel"`.
- `!` — инвертируем: **оставляем** только те объекты, у которых **нет** тега `"viewmodel"`.

### Второй фильтр — исключение поверхностей

```csharp
.WithFilter( x => x.PrefabInstanceSource?.StartsWith( "prefabs/surface/" ) is not true )
```
- `PrefabInstanceSource` — путь к исходному префабу объекта.
- `?.StartsWith(...)` — безопасная проверка: если `PrefabInstanceSource` не `null`, проверяем, начинается ли путь с `"prefabs/surface/"`.
- `is not true` — паттерн-матчинг: исключаем объекты, для которых условие **точно** `true`. Если `PrefabInstanceSource` — `null`, результат будет `null`, а `null is not true` → `true` (объект остаётся).

### Пример: что попадёт в запись

| Объект | Тег | Префаб | В записи? |
|---|---|---|---|
| Игрок | `player` | `prefabs/player.prefab` | ✅ Да |
| Автомат (вьюмодель) | `viewmodel` | `prefabs/weapons/ak47_vm.prefab` | ❌ Нет |
| Пятно крови | — | `prefabs/surface/blood.prefab` | ❌ Нет |
| Ящик | `prop` | `prefabs/props/crate.prefab` | ✅ Да |

## Результат

После создания этого файла:
- Запись демо автоматически исключает вьюмодели и декали поверхностей.
- Видео получается чище — без артефактов от первого лица.
- Настройка применяется глобально через атрибут `[DefaultMovieRecorderOptions]`.

---

Следующий шаг: [01.12 — Звук столкновений (SelfCollisionSound)](01_12_SelfCollisionSound.md)
