# 00.23 — RPC сообщения

## Что мы делаем?

Разбираем **RPC** (Remote Procedure Call) — вызов метода, который выполняется **не только у тебя, но и у других клиентов**. Это способ отправить команду «запустить вот этот код» по сети.

## Зачем нужен RPC?

Пример: игрок стреляет. Логика:

```csharp
void OnShoot()
{
    Sound.FromWorld( "shoot", WorldPosition );
    SpawnMuzzleFlash();
}
```

Если это запускается только у стреляющего — **другие игроки не услышат выстрел и не увидят вспышку**. Нужно, чтобы этот метод выполнился **у всех**.

Решение:

```csharp
void OnShoot()
{
    PlayShootFX();
}

[Rpc.Broadcast]
void PlayShootFX()
{
    Sound.FromWorld( "shoot", WorldPosition );
    SpawnMuzzleFlash();
}
```

Теперь когда я вызываю `PlayShootFX()`, движок:
1. Запустит метод локально у меня.
2. Пошлёт пакет «Клиент А просит всех выполнить `PlayShootFX`».
3. У всех остальных клиентов этот метод выполнится.

## Виды RPC

### `[Rpc.Broadcast]` — всем

Метод выполнится у всех подключённых клиентов (включая хоста).

```csharp
[Rpc.Broadcast]
public void PlaySound()
{
    Sound.FromWorld( "pop", WorldPosition );
}
```

### `[Rpc.Owner]` — только владельцу объекта

Выполнится только у владельца `GameObject`, на компоненте которого висит RPC. Если владельца нет — выполнится у хоста.

```csharp
[Rpc.Owner]
public void NotifyPickup( string itemName )
{
    ShowInventoryPopup( itemName );
}
```

Отлично для «покажи UI только тому, кого это касается».

### `[Rpc.Host]` — только хосту

```csharp
[Rpc.Host]
public void RequestKick( string target )
{
    if ( !HasKickPermission() ) return;
    KickPlayer( target );
}
```

Используется для клиентских **запросов к серверу**: клиент вызывает → выполняется на хосте, который принимает решение.

## Static RPC

Можно без компонента — просто статический метод:

```csharp
[Rpc.Broadcast]
public static void PlaySoundEverywhere( string soundName, Vector3 pos )
{
    Sound.Play( soundName, pos );
}

// Вызов откуда угодно
PlaySoundEverywhere( "boom", hitPos );
```

## Аргументы

Можно передавать все типы, которые поддерживает `[Sync]` (см. [00.24](00_24_Sync_Properties.md)): значимые типы, `string`, `Vector3`, `Rotation`, `GameObject`, `Component`, `GameResource`. Нельзя — пользовательские ссылочные классы.

```csharp
[Rpc.Broadcast]
public void Hit( GameObject attacker, Vector3 force, int damage )
{
    // будет вызвано у всех
}
```

Ссылки на `GameObject` по сети «переводятся» через сетевой ID — у каждого клиента это будет **его** копия того же объекта.

## Flags — контроль доставки

```csharp
[Rpc.Broadcast( NetFlags.Unreliable )]
public void StreamPosition( Vector3 pos )
{
    WorldPosition = pos;
}
```

| Флаг | Значение |
|---|---|
| `NetFlags.Reliable` | По умолчанию. Гарантированная доставка. Медленнее. Используй для важных событий: смерть, чат, подбор. |
| `NetFlags.Unreliable` | Может потеряться. Быстрее. Используй для «потока»: позиции, эффекты, частые события. |
| `NetFlags.SendImmediate` | Не собирать в пакет с другими — слать сразу. Для голоса / стриминга. |
| `NetFlags.DiscardOnDelay` | Выкинуть, если нельзя отправить быстро. Только для unreliable. |
| `NetFlags.HostOnly` | RPC могут вызвать только с хоста (защита). |
| `NetFlags.OwnerOnly` | Только владелец объекта может вызвать. |

Комбинируются через `|`.

## Фильтрация получателей

Вызвать RPC только для части клиентов:

```csharp
// Не слать RPC Гарри
using ( Rpc.FilterExclude( c => c.DisplayName == "Harry" ) )
{
    PlayShootFX();
}

// Слать только Гарри
using ( Rpc.FilterInclude( c => c.DisplayName == "Garry" ) )
{
    NotifyPersonal();
}
```

## Кто позвал? — `Rpc.Caller`

Внутри RPC можно спросить, **кто** инициировал вызов:

```csharp
[Rpc.Broadcast]
public void ReportScore( int score )
{
    if ( Rpc.Caller is null ) return;
    Log.Info( $"{Rpc.Caller.DisplayName} получил {score}" );
}
```

Используется для проверки прав (`if ( !Rpc.Caller.IsHost ) return;`) и атрибуции действий.

## Что RPC **не** делает

- ❌ **Не синхронизирует состояние.** Если A вызвал RPC, а в это время подключился C, C ничего не получит — RPC отправляется раз и всё. Для «состояния» — `[Sync]`.
- ❌ **Не проходит через прокси.** Вызываешь RPC на своём GameObject — всё ок. Вызываешь на прокси — тоже ок, но порядок/поведение стоит проверять осторожно.
- ❌ **Не должен содержать тяжёлых аргументов.** Не передавай картинки или большие массивы. Это пакет сети.

## Паттерны

### «Игрок нажал кнопку — скажи всем»

```csharp
void OnButtonPressed() => BroadcastButtonPress();

[Rpc.Broadcast]
void BroadcastButtonPress()
{
    Sound.FromWorld( "button.press", WorldPosition );
    DoorComponent.Open();
}
```

### «Клиент просит хоста что-то сделать»

```csharp
void ClickKick( string target ) => RequestKick( target );

[Rpc.Host]
void RequestKick( string target )
{
    // выполнится на хосте
    if ( !CanKick( Rpc.Caller, target ) ) return;
    DoKick( target );
}
```

### «Хост говорит одному игроку: держи достижение»

```csharp
// На хосте
player.NotifyAchievement( "first_kill" );

[Rpc.Owner]
public void NotifyAchievement( string id )
{
    AchievementPopup.Show( id );
}
```

## Результат

После этого этапа ты знаешь:

- ✅ Что такое RPC и чем он отличается от обычного метода.
- ✅ Разницу между `[Rpc.Broadcast]`, `[Rpc.Owner]`, `[Rpc.Host]`.
- ✅ Какие типы можно передавать в аргументах.
- ✅ Флаги `Reliable` vs `Unreliable`, `HostOnly`, `OwnerOnly`.
- ✅ Как отфильтровать получателей и узнать вызвавшего (`Rpc.Caller`).
- ✅ Что RPC — это **событие**, а не **состояние**.

---

📚 **Facepunch docs:** [networking/rpc-messages.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/rpc-messages.md)

**Следующий шаг:** [00.24 — Sync Properties (синхронизация свойств)](00_24_Sync_Properties.md)
