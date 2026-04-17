# 00.30 — Log, Assert и диагностика

## Что мы делаем?

Разбираем, как **правильно логировать, проверять предусловия и диагностировать проблемы**. Это последний файл фундамента — дальше ты идёшь в Фазу 1 писать код, и тебе понадобится выводить состояние и ловить ошибки.

## Класс `Log`

```csharp
Log.Info( "Обычное сообщение" );
Log.Warning( "Что-то подозрительное" );
Log.Error( "Что-то сломалось" );
Log.Trace( "Очень подробное отладочное" );
```

Цвета в консоли:

| Метод | Цвет | Когда |
|---|---|---|
| `Log.Info` | белый | обычная информация, «игрок зашёл», «раунд начался» |
| `Log.Warning` | жёлтый | «настройка не задана», «значение вне диапазона» |
| `Log.Error` | красный | «не смог загрузить ассет», «null там где нельзя» |
| `Log.Trace` | серый, тихий | отладочная детализация, скрывается по умолчанию |

## Интерполяция строк

Не нужно `string.Format` — есть `$"..."`:

```csharp
Log.Info( $"Игрок {player.Name} получил {damage} урона от {attacker.Name}" );
Log.Info( $"Позиция: {WorldPosition}" );
Log.Info( $"HP: {Health}/{MaxHealth} ({Health * 100f / MaxHealth:F0}%)" );
```

## Класс `Assert` (из `Sandbox.Diagnostics`)

«Утверждение» — проверка, которая **должна быть истинной**. Если нет — ошибка.

```csharp
using Sandbox.Diagnostics;

Assert.NotNull( BulletPrefab, "BulletPrefab не назначен!" );
Assert.True( Health > 0, $"Здоровье {Health} должно быть положительным" );
Assert.AreEqual( players.Count, 2, "Ожидалось ровно 2 игрока" );
```

Если проверка провалится — в консоли появится красная ошибка со стеком. Дальше код продолжит выполняться (это **не** исключение), но ты сразу увидишь, где проблема.

### Когда использовать Assert

- ✅ На **предусловия** метода: «этот параметр не должен быть null».
- ✅ На **инварианты**: «после этого шага коллекция не пустая».
- ✅ На **временную диагностику**: «хочу знать, если это вообще случится».

### Когда НЕ использовать

- ❌ Для валидации пользовательского ввода — это ожидаемая ситуация, не баг.
- ❌ Для сетевых проверок — лаг не ошибка, а норма.

## `IsValid()` — безопасная проверка

У `GameObject` и `Component` есть метод-расширение `IsValid()`, который возвращает `true` только если объект:
1. Не null.
2. Не уничтожен (`Destroy()` не вызывался).
3. Находится в рабочей сцене.

```csharp
if ( !target.IsValid() )
    return;

target.WorldPosition = newPos;
```

Это **безопаснее** чем `if ( target != null )`, потому что объект мог быть уничтожен, а ссылка на него ещё висит.

## DebugOverlay — нарисовать прямо в мире

Для визуальной отладки — не логи, а **геометрия**:

```csharp
// Сфера на 2 секунды
DebugOverlay.Sphere( pos, radius: 10f, color: Color.Red, duration: 2f );

// Линия от A до B на 1 секунду
DebugOverlay.Line( from, to, Color.Green, duration: 1f );

// Текст в мире
DebugOverlay.Text( $"HP: {Health}", pos + Vector3.Up * 80, Color.White );

// 3D-бокс
DebugOverlay.Box( center, size, Color.Blue );
```

Отлично для:
- «куда стреляет мой трейс».
- «где видит меня AI».
- «какой у меня радиус атаки».
- «где точка спавна».

## `Gizmo.Draw` — редакторный дебаг-рендер

Пока `DebugOverlay` работает в игре, `Gizmo.Draw` работает **в редакторе**, когда выделен твой компонент. Переопредели `DrawGizmos()`:

```csharp
protected override void DrawGizmos()
{
    if ( !Gizmo.IsSelected ) return;

    Gizmo.Draw.Color = Color.Yellow;
    Gizmo.Draw.LineSphere( Vector3.Zero, Radius );
}
```

Используется в Sandbox для: показать радиус действия NPC, зону триггера, направление света.

## Команды консоли для отладки

| Команда | Что |
|---|---|
| `sbox_error_reporting 1` | Подробные ошибки |
| `hotload_log 2` | Подробный отчёт про hotload |
| `net_graph 1` | Сетевая статистика |
| `fps_max 60` | Ограничить FPS (для тестов детерминизма) |
| `timescale 0.3` | Замедлить игру (если не заблокировано) |

## `Log.Info` с категорией

Чтобы отфильтровать свои логи в консоли:

```csharp
using Sandbox.Diagnostics;

private static readonly Logger Logger = new Logger( "Weapon" );

void Shoot()
{
    Logger.Info( "Выстрел" );      // в консоли с префиксом [Weapon]
    Logger.Warning( "Без патронов" );
}
```

Потом `find Weapon` — увидишь все свои сообщения, не утонешь в чужих.

## Паттерн: безопасный доступ

Когда ты не уверен, что объект жив:

```csharp
public override void OnUpdate()
{
    // Не читаешь Target напрямую — IsValid() ловит destroy
    if ( !Target.IsValid() )
    {
        Target = null;
        return;
    }

    WorldRotation = Rotation.LookAt( Target.WorldPosition - WorldPosition );
}
```

## Паттерн: один раз лог при смене состояния

Не хочешь засорять лог одинаковыми строками каждый кадр:

```csharp
private bool _wasReady;

protected override void OnUpdate()
{
    if ( IsReady && !_wasReady )
    {
        Log.Info( "Теперь готов" );
        _wasReady = true;
    }
    if ( !IsReady && _wasReady )
    {
        Log.Info( "Больше не готов" );
        _wasReady = false;
    }
}
```

## Результат

После этого этапа ты знаешь:

- ✅ `Log.Info`/`Warning`/`Error`/`Trace` — уровни логирования.
- ✅ `Assert.NotNull`/`True`/`AreEqual` — для проверки предусловий.
- ✅ Что `IsValid()` безопаснее `!= null` для `GameObject`/`Component`.
- ✅ Как рисовать отладку в мире (`DebugOverlay`) и в редакторе (`Gizmo.Draw`).
- ✅ Полезные консольные команды для диагностики.

---

🎉 **Фаза 0.5 завершена!**

Теперь у тебя есть крепкий фундамент. Дальше — [Фаза 1 — Фундамент проекта](01_01_Assembly.md), и весь её код будет прозрачен: ты знаешь про `GameObject`, `Component`, `[Property]`, `[Sync]`, `IsProxy`, Razor, `HudPainter`, `GameResource`, hotload и логирование.

---

📚 **Facepunch docs:** [code/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/code/index.md)

**Следующий шаг:** [01.01 — Assembly.cs (глобальные using)](01_01_Assembly.md)
