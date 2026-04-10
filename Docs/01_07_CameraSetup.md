# 01.07 — Настройка камеры (CameraSetup)

## Что мы делаем?

Этот файл создаёт **систему настройки камеры** с тремя этапами обработки. Перед каждым кадром рендеринга камера проходит через «конвейер» из трёх шагов:

1. **PreSetup** — подготовительные эффекты (до вьюмодели).
2. **Setup** — размещение вьюмодели (модели оружия от первого лица).
3. **PostSetup** — финальные эффекты (включая вьюмодель).

Любой компонент в проекте может «подписаться» на эти этапы через интерфейс `ICameraSetup` и влиять на камеру — добавлять тряску, отдачу, покачивание и т.д.

## Как это работает внутри движка?

### `ISceneEvent<T>`

Интерфейс `ISceneEvent<T>` — это система событий s&box. Любой компонент, реализующий `ICameraSetup`, автоматически получает уведомления через метод `Post()`. Это паттерн «наблюдатель» (Observer), встроенный в движок.

### `OnPreRender`

Метод `OnPreRender()` вызывается движком **каждый кадр**, непосредственно перед отрисовкой. Это идеальное место для настройки камеры, потому что все изменения применяются до того, как кадр будет нарисован.

### Порядок вызова

`ICameraSetup.Post(x => x.PreSetup(cc))` — вызывает `PreSetup` у всех зарегистрированных обработчиков. Затем `Setup`, затем `PostSetup`. Порядок гарантирован.

## Создай файл

Путь: `Code/Utility/CameraSetup.cs`

```csharp

/// <summary>
/// Creates a bunch of callbacks, allowing finer control over applying camera effects
/// </summary>
public sealed class CameraSetup : Component
{
	protected override void OnPreRender()
	{
		var cc = GetComponent<CameraComponent>();
		if ( cc is null ) return;

		ICameraSetup.Post( x => x.PreSetup( cc ) );
		ICameraSetup.Post( x => x.Setup( cc ) );
		ICameraSetup.Post( x => x.PostSetup( cc ) );
	}
}


public interface ICameraSetup : ISceneEvent<ICameraSetup>
{
	// Effects before viewmodel
	public void PreSetup( CameraComponent cc ) { }

	// Place viewmodel
	public void Setup( CameraComponent cc ) { }

	// Effects including viewmodel
	public void PostSetup( CameraComponent cc ) { }
}
```

## Разбор кода

### Компонент `CameraSetup`

```csharp
public sealed class CameraSetup : Component
```
- `sealed` — класс нельзя наследовать (финальный).
- `Component` — базовый класс компонентов s&box. Прикрепляется к `GameObject`.

### Метод `OnPreRender`

```csharp
protected override void OnPreRender()
{
    var cc = GetComponent<CameraComponent>();
    if ( cc is null ) return;
```
1. Каждый кадр ищем `CameraComponent` на том же объекте.
2. Если камеры нет — выходим (защита от ошибок).

```csharp
    ICameraSetup.Post( x => x.PreSetup( cc ) );
    ICameraSetup.Post( x => x.Setup( cc ) );
    ICameraSetup.Post( x => x.PostSetup( cc ) );
}
```
Последовательно вызываем три этапа у **всех** компонентов, реализующих `ICameraSetup`.

### Интерфейс `ICameraSetup`

```csharp
public interface ICameraSetup : ISceneEvent<ICameraSetup>
{
    public void PreSetup( CameraComponent cc ) { }
    public void Setup( CameraComponent cc ) { }
    public void PostSetup( CameraComponent cc ) { }
}
```
- Наследует `ISceneEvent<ICameraSetup>` — интеграция с системой событий движка.
- Все три метода имеют **реализации по умолчанию** (пустые тела `{ }`). Компонент может переопределить только нужные.
- Комментарии объясняют назначение:
  - `PreSetup` — эффекты камеры до вьюмодели (тряска экрана).
  - `Setup` — размещение вьюмодели.
  - `PostSetup` — эффекты после (включая вьюмодель).

### Пример использования

```csharp
public class MyShake : Component, ICameraSetup
{
    void ICameraSetup.PostSetup( CameraComponent cc )
    {
        // Добавляем тряску камеры
        cc.WorldRotation *= Rotation.From( 0.5f, 0, 0 );
    }
}
```

## Результат

После создания этого файла:
- Камера имеет трёхэтапный конвейер обработки (PreSetup → Setup → PostSetup).
- Любой компонент может реализовать `ICameraSetup` и влиять на камеру.
- Система поддерживает множество независимых эффектов одновременно.
- Это фундамент для тряски камеры, отдачи оружия и других визуальных эффектов.

---

Следующий шаг: [01.08 — Трассер пуль (Tracer)](01_08_Tracer.md)
