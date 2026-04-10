# 01.02 — Дополнения к движку (EngineAdditions)

## Что мы делаем?

Этот файл расширяет возможности движка s&box двумя вещами:

1. **Интерфейс `IDefinitionResource`** — контракт для любых ресурсов (предметы, оружие, скины), которые должны иметь название и описание.
2. **Метод `FindNetworkRoot()`** — позволяет любому `GameObject` найти свой «сетевой корень» — объект верхнего уровня, который отвечает за синхронизацию по сети.

Представьте, что у вас есть кукла (персонаж) с рукой, в которой зажат пистолет. Пистолет — это дочерний объект. Но по сети синхронизируется весь персонаж целиком. `FindNetworkRoot()` — это способ от пистолета «подняться наверх» и найти персонажа.

## Как это работает внутри движка?

### Интерфейс `IDefinitionResource`

Интерфейсы в C# — это «контракты». Любой класс, реализующий `IDefinitionResource`, обязан иметь свойства `Title` и `Description`. Это позволяет единообразно работать с любыми ресурсами: оружием, предметами, картами и т.д.

### Extension-метод `FindNetworkRoot()`

В s&box `GameObject` могут быть вложены друг в друга (родитель → дочерний). Каждый объект может иметь `NetworkMode`:
- `NetworkMode.Object` — объект сам является «сетевым корнем» и синхронизируется.
- Другие режимы — объект зависит от родителя.

Метод рекурсивно поднимается по дереву объектов (`go.Parent`) до тех пор, пока не найдёт объект с `NetworkMode.Object`.

## Создай файл

Путь: `Code/EngineAdditions.cs`

```csharp
namespace Sandbox;

public interface IDefinitionResource
{
	public string Title { get; set; }
	public string Description { get; set; }
}

public static class EngineAdditions
{
	extension( GameObject go )
	{
		public GameObject FindNetworkRoot()
		{
			if ( !go.IsValid() ) return null;

			if ( go.NetworkMode == NetworkMode.Object ) return go;
			return go.Parent?.FindNetworkRoot();
		}
	}
}
```

## Разбор кода

### Пространство имён

```csharp
namespace Sandbox;
```
Файл помещён в пространство `Sandbox` — то же, что и сам движок. Это позволяет «расширять» движок так, будто код — его часть.

### Интерфейс `IDefinitionResource`

```csharp
public interface IDefinitionResource
{
    public string Title { get; set; }
    public string Description { get; set; }
}
```
- `interface` — контракт. Любой класс, реализующий его, **обязан** иметь `Title` и `Description`.
- `{ get; set; }` — свойство можно читать и записывать.

### Класс `EngineAdditions`

```csharp
public static class EngineAdditions
```
`static class` — утилитарный класс, экземпляр которого создавать не нужно.

### Extension-метод

```csharp
extension( GameObject go )
{
    public GameObject FindNetworkRoot()
    {
```
Конструкция `extension(GameObject go)` — это **расширение типа** (extension method в стиле s&box). Она добавляет метод `FindNetworkRoot()` ко всем объектам `GameObject`.

### Логика поиска

```csharp
if ( !go.IsValid() ) return null;
```
Проверка: объект существует и не уничтожен? Если нет — возвращаем `null`.

```csharp
if ( go.NetworkMode == NetworkMode.Object ) return go;
```
Если текущий объект — сетевой корень, возвращаем его.

```csharp
return go.Parent?.FindNetworkRoot();
```
Иначе — рекурсивно ищем у родителя. Оператор `?.` защищает от `null`: если родителя нет, вернётся `null`.

## Результат

После создания этого файла:
- Любой ресурс (оружие, предмет) может реализовать `IDefinitionResource` и получить единообразные `Title`/`Description`.
- Любой `GameObject` может вызвать `.FindNetworkRoot()`, чтобы найти объект, отвечающий за сетевую синхронизацию.
- Это фундамент для мультиплеерной логики проекта.

---

Следующий шаг: [01.03 — Расширения векторов (Extensions)](01_03_Extensions.md)
