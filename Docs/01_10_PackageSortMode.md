# 01.10 — Сортировка пакетов (PackageSortMode)

## Что мы делаем?

Этот файл определяет **перечисление (enum)** для режимов сортировки пакетов (модов, карт, инструментов) в каталоге s&box, а также метод-расширение для преобразования enum в строковый идентификатор API.

Когда игрок открывает браузер пакетов и выбирает «Популярные», «Новые», «Трендовые» или «Случайные» — именно этот enum определяет, какой фильтр применить.

## Как это работает внутри движка?

### Enum

Перечисление (`enum`) — это набор именованных констант. Вместо строк `"popular"`, `"newest"` мы используем типобезопасные значения `PackageSortMode.Popular`, `PackageSortMode.Newest`. Это защищает от опечаток и делает код читабельнее.

### Switch expression

```csharp
return sortMode switch { ... };
```
Это **switch expression** из C# 8 — компактная форма `switch`, которая возвращает значение. Каждый вариант сопоставляется со строкой-идентификатором для API.

### Паттерн `_` (discard)

```csharp
_ => "popular"
```
`_` — «всё остальное». Если передано неизвестное значение — по умолчанию возвращаем `"popular"`.

## Создай файл

Путь: `Code/Utility/PackageSortMode.cs`

```csharp
public enum PackageSortMode
{
	Popular,
	Newest,
	Trending,
	Random
}

public static class PackageSortModeExtensions
{
	public static string ToIdentifier( this PackageSortMode sortMode )
	{
		return sortMode switch
		{
			PackageSortMode.Popular => "popular",
			PackageSortMode.Newest => "newest",
			PackageSortMode.Trending => "trending",
			PackageSortMode.Random => "random",
			_ => "popular"
		};
	}
}
```

## Разбор кода

### Перечисление `PackageSortMode`

```csharp
public enum PackageSortMode
{
    Popular,    // Популярные
    Newest,     // Новые
    Trending,   // Трендовые
    Random      // Случайные
}
```
Четыре режима сортировки. По умолчанию `Popular = 0`, `Newest = 1`, `Trending = 2`, `Random = 3`.

### Класс расширений

```csharp
public static class PackageSortModeExtensions
```
Статический класс с методами-расширениями для `PackageSortMode`.

### Метод `ToIdentifier`

```csharp
public static string ToIdentifier( this PackageSortMode sortMode )
```
- `this PackageSortMode sortMode` — метод-расширение. Вызывается как `mySort.ToIdentifier()`.
- Возвращает строку-идентификатор для использования в API-запросах.

```csharp
return sortMode switch
{
    PackageSortMode.Popular => "popular",
    PackageSortMode.Newest => "newest",
    PackageSortMode.Trending => "trending",
    PackageSortMode.Random => "random",
    _ => "popular"                        // Значение по умолчанию
};
```

### Пример использования

```csharp
var mode = PackageSortMode.Trending;
var apiParam = mode.ToIdentifier(); // "trending"

// Используется в запросе к API:
// GET /api/packages?sort=trending
```

## Результат

После создания этого файла:
- Проект имеет типобезопасный enum для режимов сортировки пакетов.
- Метод `.ToIdentifier()` преобразует enum в строку для API.
- Код защищён от опечаток в строках сортировки.

---

Следующий шаг: [01.11 — Запись демо (DemoRecording)](01_11_DemoRecording.md)
