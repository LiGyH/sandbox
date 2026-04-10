# 01.09 — Система куки (CookieSource)

## Что мы делаем?

Этот файл создаёт систему **автоматического сохранения и загрузки настроек** через «куки» движка. Вместо того чтобы вручную сохранять каждое свойство, вы реализуете интерфейс `ICookieSource` — и система сама находит все публичные свойства, сериализует их в JSON и сохраняет в `Game.Cookies`.

Аналогия: представьте форму с полями (громкость, чувствительность мыши, имя). Вы нажали «Сохранить» — система автоматически прошлась по всем полям и запомнила их. Нажали «Загрузить» — все поля заполнились прежними значениями.

## Как это работает внутри движка?

### `Game.Cookies`

`Game.Cookies` — встроенное хранилище движка s&box для пар «ключ-значение». Хранится локально на компьютере игрока и переживает перезапуск игры.

### `TypeLibrary`

`TypeLibrary` — система рефлексии s&box. Позволяет в рантайме получить информацию о типе: его свойства, атрибуты, методы. Используется для автоматического обнаружения свойств, которые нужно сохранить.

### Extension methods (стиль s&box)

```csharp
extension( ICookieSource source ) { ... }
```
Это расширения типа в стиле s&box. Методы `SaveCookies()` и `LoadCookies()` добавляются ко всем объектам, реализующим `ICookieSource`.

## Создай файл

Путь: `Code/Utility/CookieSource.cs`

```csharp
﻿using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// We want to save the properties of this instance as cookies.
/// </summary>
public interface ICookieSource
{
	/// <summary>
	/// Prefix to put before the names of any cookies, without a trailing period.
	/// For example: <c>"tool.balloon"</c>
	/// </summary>
	string CookiePrefix { get; }
}

/// <summary>
/// Extension methods for <see cref="ICookieSource"/>.
/// </summary>
public static class CookieSourceExtensions
{
	private static bool IsCookieProperty( PropertyDescription property )
	{
		// TODO: maybe we want to support static properties too?

		if ( property.IsStatic ) return false;
		if ( !property.HasAttribute<PropertyAttribute>() ) return false;
		if ( property.HasAttribute<JsonIgnoreAttribute>() ) return false;
		if ( !property.CanWrite || !property.CanRead ) return false;

		return true;
	}

	private static string GetCookieName( PropertyDescription property, string prefix )
	{
		var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name.ToLowerInvariant();

		return !string.IsNullOrEmpty( prefix ) ? $"{prefix}.{name}" : name;
	}

	extension( ICookieSource source )
	{
		private IEnumerable<(string CookieName, PropertyDescription Property)> GetCookieProperties()
		{
			var typeDesc = TypeLibrary.GetType( source.GetType() );
			if ( typeDesc is null ) return [];

			var prefix = source.CookiePrefix;

			return typeDesc.Properties.Where( IsCookieProperty )
				.Select( x => (GetCookieName( x, prefix ), x) );
		}

		/// <summary>
		/// Saves any instance properties with a <see cref="PropertyAttribute"/>, excluding any
		/// with <see cref="JsonIgnoreAttribute"/>, into <see cref="Game.Cookies"/>.
		/// </summary>
		public void SaveCookies()
		{
			foreach ( var (cookieName, property) in source.GetCookieProperties() )
			{
				try
				{
					var cookieValue = property.GetValue( source );

					Game.Cookies.SetString( cookieName, JsonSerializer.Serialize( cookieValue, property.PropertyType ) );
				}
				catch ( Exception ex )
				{
					Log.Warning( ex, $"Exception while saving cookie \"{cookieName}\"." );
				}
			}
		}

		/// <summary>
		/// Loads any instance properties with a <see cref="PropertyAttribute"/>, excluding any
		/// with <see cref="JsonIgnoreAttribute"/>, from <see cref="Game.Cookies"/>.
		/// </summary>
		public void LoadCookies()
		{
			foreach ( var (cookieName, property) in source.GetCookieProperties() )
			{
				if ( !Game.Cookies.TryGetString( cookieName, out var jsonString ) ) continue;

				try
				{
					var cookieValue = JsonSerializer.Deserialize( jsonString, property.PropertyType );

					property.SetValue( source, cookieValue );
				}
				catch ( Exception ex )
				{
					Log.Warning( ex, $"Exception while loading cookie \"{cookieName}\"." );
				}
			}
		}
	}
}
```

## Разбор кода

### Интерфейс `ICookieSource`

```csharp
public interface ICookieSource
{
    string CookiePrefix { get; }
}
```
Единственное требование — указать **префикс** для имён куки. Например, `"tool.balloon"` → куки будут называться `"tool.balloon.color"`, `"tool.balloon.size"` и т.д.

### Фильтрация свойств — `IsCookieProperty`

```csharp
private static bool IsCookieProperty( PropertyDescription property )
{
    if ( property.IsStatic ) return false;                        // Статические — нет
    if ( !property.HasAttribute<PropertyAttribute>() ) return false; // Без [Property] — нет
    if ( property.HasAttribute<JsonIgnoreAttribute>() ) return false; // С [JsonIgnore] — нет
    if ( !property.CanWrite || !property.CanRead ) return false;     // Readonly — нет
    return true;
}
```
Свойство сохраняется в куки, только если:
1. Это **экземплярное** свойство (не `static`).
2. Помечено атрибутом `[Property]`.
3. **Не** помечено `[JsonIgnore]`.
4. Поддерживает чтение **и** запись.

### Генерация имени куки — `GetCookieName`

```csharp
var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
    ?? property.Name.ToLowerInvariant();
```
Имя куки берётся из атрибута `[JsonPropertyName("...")]`, а если его нет — из имени свойства в нижнем регистре. Затем добавляется префикс.

### Сохранение — `SaveCookies()`

Для каждого подходящего свойства:
1. Получаем текущее значение через рефлексию.
2. Сериализуем в JSON-строку.
3. Сохраняем в `Game.Cookies` с уникальным именем.
4. При ошибке — логируем предупреждение, но не останавливаемся.

### Загрузка — `LoadCookies()`

Обратный процесс:
1. Для каждого свойства пробуем найти куки по имени.
2. Если нашли — десериализуем JSON обратно в нужный тип.
3. Устанавливаем значение через рефлексию.
4. При ошибке — логируем и пропускаем.

### Пример использования

```csharp
public class BalloonTool : Component, ICookieSource
{
    public string CookiePrefix => "tool.balloon";

    [Property] public Color Color { get; set; } = Color.Red;
    [Property] public float Size { get; set; } = 1.0f;
    [Property, JsonIgnore] public int TempCounter { get; set; } // Не сохраняется!

    void OnEnable()
    {
        this.LoadCookies(); // Загружаем сохранённые значения
    }

    void OnDisable()
    {
        this.SaveCookies(); // Сохраняем при выключении
    }
}
```

## Результат

После создания этого файла:
- Любой компонент может автоматически сохранять/загружать свои настройки, реализовав `ICookieSource`.
- Система использует рефлексию — не нужно вручную сохранять каждое свойство.
- Поддерживается фильтрация через `[JsonIgnore]` и кастомные имена через `[JsonPropertyName]`.
- Ошибки обрабатываются безопасно — одно битое свойство не ломает остальные.

---

Следующий шаг: [01.10 — Сортировка пакетов (PackageSortMode)](01_10_PackageSortMode.md)
