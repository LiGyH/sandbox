# 27.05 — User Permissions: админы, claims, проверки

## Что мы делаем?

Учим dedicated-сервер **различать обычного игрока и админа**: настраиваем `users/config.json`, придумываем свои claim-ы, и в коде безопасно вызываем `Connection.HasPermission(...)` перед любым «опасным» действием (бан, кик, чейнджмап, выдача валюты).

> 📚 Источник: [Facepunch/sbox-docs → dedicated-servers/user-permissions.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/dedicated-servers/user-permissions.md).

## Где живут права

В корне сервера есть файл:

```
Dedicated Server/users/config.json
```

Формат — массив объектов «один объект на пользователя»:

```json
[
    {
        "SteamId": "76561198000000001",
        "Name": "ADMIN_BOSS",
        "Claims": [ "kick", "ban", "restart", "give_money" ]
    },
    {
        "SteamId": "76561198000000002",
        "Name": "MODERATOR",
        "Claims": [ "kick" ]
    }
]
```

Поля:

| Поле | Что значит |
|---|---|
| `SteamId` | Steam ID 64 (тот, что начинается на `7656...`). Узнать можно через [steamid.io](https://steamid.io/). |
| `Name` | Подпись «для людей», движком не используется, нужна тебе для удобства. |
| `Claims` | Массив строк — что разрешено этому игроку. |

> ✋ После правки `users/config.json` сервер **перечитывает файл** при старте. Если правил «налету», убедись, что движок поддерживает горячую перезагрузку (зависит от версии). Безопаснее — рестарт сервера.

## Что такое Claim

**Claim — это просто строка.** Никаких заранее определённых значений у движка нет (за исключением того, что сам ты введёшь). Это значит, что ты **сам придумываешь** систему прав:

- `kick`, `ban`, `restart` — общепринятые «модерские».
- `give_money`, `noclip`, `god` — для RP-сервера.
- `pickup_admin_loot`, `spawn_boss` — для PvE.

Главное правило именования: **глагол_объект** или короткое **глагол** (`kick`, `give_money`). Не пихай в claim условия (`kick_only_low_rank` — лучше делать как `kick` + проверки в коде).

## Проверка в коде: `Connection.HasPermission`

Любая «опасная» серверная команда должна **первой строкой** проверять права:

```csharp
[ConCmd.Server( "rp_kick" )]
public static void Kick( int connectionId, string reason )
{
    var caller = Rpc.Caller; // кто вызвал
    if ( caller is null ) return;

    if ( !caller.HasPermission( "kick" ) )
    {
        Log.Warning( $"[Security] {caller.DisplayName} попытался kick без прав" );
        return;
    }

    var target = Connection.All.FirstOrDefault( c => c.Id == connectionId );
    if ( target is null ) return;

    Log.Info( $"[Admin] {caller.DisplayName} кикает {target.DisplayName}: {reason}" );
    target.Kick( reason );
}
```

Что важно:

- ✅ Хост (или dedicated сам по себе) **по умолчанию имеет все claim-ы**. Тестируй на втором аккаунте.
- ✅ Если `caller` — `null`, это вызвал сам сервер (например, по таймеру) — пропускай проверку или ставь свою.
- ✅ Логируй и **разрешённые**, и **отклонённые** попытки. Это первая линия аудита.

## Свой helper для типовых проверок

Чтобы не таскать `if (!caller.HasPermission(...))` в каждой команде, заведи `AdminUtil.Server.cs`:

```csharp
// AdminUtil.Server.cs
public static class AdminUtil
{
    public static bool RequirePermission( Connection caller, string claim )
    {
        if ( caller is null ) return true; // сервер сам себе админ

        if ( caller.HasPermission( claim ) )
            return true;

        Log.Warning( $"[Security] {caller.DisplayName} лишён {claim}" );
        Chat.SendDirect( caller, "У тебя нет прав на это действие" );
        return false;
    }
}
```

Тогда:

```csharp
[ConCmd.Server( "rp_givemoney" )]
public static void GiveMoney( int targetConnId, int amount )
{
    if ( !AdminUtil.RequirePermission( Rpc.Caller, "give_money" ) )
        return;

    // ... логика
}
```

## Часто встречающиеся claim-ы (рекомендация)

| Claim | На что разрешает |
|---|---|
| `kick` | Кикнуть игрока (мягко) |
| `ban` | Забанить (см. [24.01](24_01_BanSystem.md)) |
| `restart` | Перезапустить сервер / карту |
| `changemap` | Сменить карту |
| `noclip` | Включить себе noclip |
| `god` | Бессмертие |
| `give_*` | Выдача валюты, оружия, предметов (`give_money`, `give_weapon`) |
| `cleanup` | Очистка пропов всех игроков |
| `manage_props` | Удалить чужие пропы |

Это именно **рекомендация**: придумывай свои, документируй в `README.md` сервера.

## Что **нельзя** делать

- ❌ **Не клади админский Steam ID в код.** Только в `users/config.json`. Любая «зашитая» проверка `if (steamId == 7656...)` — антипаттерн: меняется состав админов — переcборка.
- ❌ **Не доверяй клиенту при проверке.** В UI можно скрыть кнопку «бан», если у игрока нет `ban`, но **на сервере всё равно** проверь `HasPermission` — клиент мог отправить ConCmd напрямую через консоль.
- ❌ **Не используй один claim на всё.** `admin = true/false` плох. Гранулярные claim-ы позволяют выдавать «модератору только kick», «дев-админу только give_money» — это и удобно, и безопасно.
- ❌ **Не реплицируй права на клиент.** Из официальной доки: *«Permissions are not networked right now, so only the host can check if a connection has a specific permission»*. Проверки **только на сервере**. Если хочешь скрыть UI — отдельным `[Sync] bool IsModerator` после проверки `HasPermission`.

## Пример: показывать/скрывать кнопку «Бан» в UI

Сервер при коннекте проставляет публичный флаг:

```csharp
// Player.Server.cs
public partial class Player
{
    [Sync] public bool IsModerator { get; set; }

    protected override void OnStart()
    {
        if ( !Networking.IsHost ) return;
        var conn = Network.Owner;
        IsModerator = conn?.HasPermission( "kick" ) ?? false;
    }
}
```

Клиент:

```razor
@if ( Player.Local.IsModerator )
{
    <button onclick=@OnKickClicked>Кикнуть</button>
}
```

Но **в самом ConCmd.Server** обязательно ещё раз `HasPermission( "kick" )` — клиент мог соврать про `IsModerator` (это всего лишь sync-поле, на которое влияет только сервер, но защита в глубину не помешает).

## Аудит: лог всех админ-действий

Для RP/MMO/любого «серьёзного» сервера заведи отдельный лог:

```csharp
public static class AuditLog
{
    public static void Action( Connection actor, string action, string details = "" )
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] " +
                   $"{actor?.DisplayName} ({actor?.SteamId}) " +
                   $"-> {action} {details}";
        Log.Info( $"[Audit] {line}" );
        // Дополнительно — пиши в файл/БД через #if SERVER (см. 27.04)
    }
}
```

Применение:

```csharp
AuditLog.Action( Rpc.Caller, "kick", $"target={target.SteamId} reason={reason}" );
```

Через неделю работы будешь рад: 90% жалоб «админ кикнул без причины» решаются одной командой `grep`.

## Что важно запомнить

- Права хранятся в `users/config.json` рядом с `sbox-server.exe`.
- Claim — это просто строка, систему прав ты придумываешь сам.
- Хост всегда имеет все claim-ы; для тестов нужен второй аккаунт.
- Проверка прав — **только на сервере** через `Connection.HasPermission(...)`.
- Никогда не доверяй клиенту: проверь в `[ConCmd.Server]`, даже если UI «спрятан».
- Логируй и разрешённые, и отклонённые попытки.

## Что дальше?

В [27.06](27_06_Network_Internals.md) — погружаемся **под капот сети**: tick, snapshot, как устроены `[Sync]`-обновления, RPC и владение, и где упирается производительность.
