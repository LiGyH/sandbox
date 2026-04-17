# 🖥️ Toolgun.Screen.cs и Toolgun.Effects.cs — Экран и эффекты тулгана

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём **два файла** — расширения `partial class Toolgun`:
1. `Toolgun.Screen.cs` — отрисовка содержимого на экранчике viewmodel (маленький экран на модели пушки)
2. `Toolgun.Effects.cs` — эффекты переключения инструмента и ссылки на префабы эффектов

## Зачем это нужно?

### Toolgun.Screen.cs
Когда игрок держит тулган, на модели оружия есть маленький экран. На нём отображается иконка и название текущего инструмента. `DrawScreenContent` делегирует отрисовку активному `ToolMode`.

### Toolgun.Effects.cs
- **`SuccessImpactEffect`** — префаб эффекта удара (частицы при попадании)
- **`SuccessBeamEffect`** — префаб эффекта луча (от дула к точке)
- **`SwitchToolMode()`** — анимация переключения инструмента (переключает `firing_mode` на viewmodel)

## Как это работает внутри движка?

### Экран на viewmodel

```
Toolgun.OnControl()
  └── UpdateViewmodelScreen()   (наследуется от ScreenWeapon)
        └── DrawScreenContent() (переопределяем здесь)
              └── currentMode.DrawScreen() (ToolMode рисует своё содержимое)
```

`ScreenWeapon` (из фазы 7) предоставляет механизм рендеринга в текстуру, которая затем отображается на модели оружия. `DrawScreenContent` — виртуальный метод, который мы переопределяем.

### Анимация переключения

```csharp
ping = !ping;
WeaponModel?.Renderer?.Set( "firing_mode", ping ? 1 : 0 );
```

Аниматор модели оружия имеет параметр `firing_mode` (0 или 1). Каждое переключение меняет его, что вызывает анимацию на viewmodel.

## Создай файл №1

📄 `Code/Weapons/ToolGun/Toolgun.Screen.cs`

```csharp
﻿using Sandbox.Rendering;

public partial class Toolgun : ScreenWeapon
{
	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawScreen( rect, paint );
	}
}
```

## Создай файл №2

📄 `Code/Weapons/ToolGun/Toolgun.Effects.cs`

```csharp
﻿public partial class Toolgun : ScreenWeapon
{
	[Header( "Effects" )]
	[Property] public GameObject SuccessImpactEffect { get; set; }
	[Property] public GameObject SuccessBeamEffect { get; set; }

	bool ping = false;
	public void SwitchToolMode()
	{
		ping = !ping;
		WeaponModel?.Renderer?.Set( "firing_mode", ping ? 1 : 0 );
	}
}
```

## Разбор кода

### Toolgun.Screen.cs

| Элемент | Описание |
|---------|----------|
| `DrawScreenContent` | Переопределение метода из `ScreenWeapon` |
| `GetCurrentMode()` | Получает активный `ToolMode` |
| `DrawScreen(rect, paint)` | Вызывает метод рисования текущего инструмента |

### Toolgun.Effects.cs

| Элемент | Описание |
|---------|----------|
| `SuccessImpactEffect` | Префаб, клонируемый в месте попадания (настраивается в редакторе) |
| `SuccessBeamEffect` | Префаб луча от дула к точке (настраивается в редакторе) |
| `ping` | Флаг-переключатель для анимации (`true`/`false` → `1`/`0`) |
| `SwitchToolMode()` | Вызывается из `BroadcastSwitchToolMode` при смене инструмента |

## Итого: полная структура Toolgun

После создания всех файлов `Toolgun` собирается из 3 partial-файлов:

```
Toolgun (partial class)
  ├── Toolgun.cs          — основная логика (создание, переключение, управление)
  ├── Toolgun.Screen.cs   — отрисовка экрана viewmodel
  └── Toolgun.Effects.cs  — ссылки на эффекты + анимация переключения
```

## Что проверить?

Для работы Toolgun нужно:
1. Создать префаб оружия в редакторе с компонентами `Toolgun`, `HighlightOutline` и т.д.
2. Назначить `SuccessImpactEffect` и `SuccessBeamEffect` в инспекторе
3. Создать viewmodel с экраном и аниматором

---

➡️ Следующий шаг: [09_08 — IToolgunEvent.cs](09_08_IToolgunEvent.md)
