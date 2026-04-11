# 🍪 ToolMode.Cookies.cs — Система сохранения настроек инструмента

## Что мы делаем?

Создаём файл `ToolMode.Cookies.cs` — расширение `partial class ToolMode`, которое реализует интерфейс `ICookieSource`. Это позволяет каждому инструменту автоматически сохранять и загружать свои настройки (cookies).

## Зачем это нужно?

Когда игрок настраивает инструмент (например, выбирает жёсткость верёвки = 5), эта настройка должна **сохраниться** между переключениями инструментов. `ICookieSource` — механизм движка для этого:

1. При **включении** инструмента (`OnEnabled`) вызывается `this.LoadCookies()` — все свойства с `[Property]` восстанавливаются
2. При **выключении** (`OnDisabled`) вызывается `this.SaveCookies()` — все свойства сохраняются

## Как это работает внутри движка?

### Интерфейс ICookieSource

Движок s&box определяет `ICookieSource` с одним свойством:
- `CookiePrefix` — строка-префикс, по которой группируются настройки

### Формат ключей

Для инструмента `Rope` префикс будет `"tool.rope"`.
Если у `Rope` есть свойство `Slack`, то полный ключ в cookie-хранилище будет `"tool.rope.slack"`.

### Где хранятся cookies?

Cookie-хранилище движка сохраняет данные локально на клиенте (аналог `LocalStorage` в браузере). Данные сохраняются между сессиями.

## Ссылки на движок

- [`ICookieSource`](https://github.com/LiGyH/sbox-public) — интерфейс для сохранения настроек
- `LoadCookies()` / `SaveCookies()` — методы расширения, определённые в утилите `CookieSource.cs` (см. фазу 1)

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.Cookies.cs`

```csharp
﻿partial class ToolMode : ICookieSource
{
	public virtual string CookiePrefix => $"tool.{GetType().Name.ToLowerInvariant()}";
}
```

## Разбор кода построчно

1. `partial class ToolMode` — расширяем тот же класс, что и в `ToolMode.cs`
2. `: ICookieSource` — реализуем интерфейс для cookie-системы
3. `CookiePrefix` — виртуальное свойство, которое формирует префикс из имени типа. Например:
   - `Rope` → `"tool.rope"`
   - `Weld` → `"tool.weld"`
   - `BallSocket` → `"tool.ballsocket"`

## Что проверить?

Этот файл очень маленький, но критически важен. Без него `OnEnabled()` и `OnDisabled()` в `ToolMode.cs` не будут работать (там вызываются `LoadCookies()` и `SaveCookies()`).

---

➡️ Следующий шаг: [09_03 — ToolMode.Effects.cs](09_03_ToolMode_Effects.md)
