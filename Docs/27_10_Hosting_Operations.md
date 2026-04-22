# 27.10 — Эксплуатация: хостинг, мониторинг, обновление, защита

## Что мы делаем?

Завершаем Фазу 27 практическим блоком: где **поставить** dedicated, как держать его **живым** 24/7, как **обновлять** код без долгого даунтайма, что бэкапить и как защититься от типичных угроз. Дальше идёт уже системное администрирование, не геймдев.

## 1. Где хостить

| Вариант | Плюсы | Минусы | Когда подходит |
|---|---|---|---|
| Дома, на своём ПК | бесплатно | пинг, дроп интернета, NAT | тесты с друзьями |
| VPS (Hetzner, OVH, DigitalOcean) | дёшево, гибко | shared CPU, сосед-шумный | до 64 игроков |
| Bare-metal (Hetzner Auction, OVH Game) | максимум CPU/IO | дороже, нужен админ | 64+ игроков, RP |
| Игровой хостинг (Pingperfect, GameServerKings) | «1 клик», мониторинг | дороже, меньше контроля, не всегда есть s&box | начинающие |

Минимальные требования (ориентир):

- 64 игрока, шутер: **4 vCPU**, **8 ГБ ОЗУ**, **100 Мбит**.
- 128 игроков, BR: **8 ядер**, **16 ГБ**, **1 Гбит**.
- 250 игроков, RP: **12+ ядер**, **32 ГБ**, **1 Гбит**, **NVMe SSD**.

**Локация.** Близко к большинству игроков. Для RU-аудитории — Москва/Франкфурт/Хельсинки.

## 2. Демонизация: автозапуск и перезапуск

### Linux (рекомендуется): systemd

`/etc/systemd/system/sbox-server@.service`:

```ini
[Unit]
Description=s&box Dedicated Server (instance %i)
After=network-online.target

[Service]
Type=simple
User=sbox
Group=sbox
WorkingDirectory=/home/sbox/server
EnvironmentFile=/etc/sbox.env
ExecStart=/home/sbox/server/sbox-server +game your.gameident +port 2701%i +net_query_port 2702%i +hostname "Server #%i"
Restart=on-failure
RestartSec=10s
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
```

Команды:

```bash
sudo systemctl enable --now sbox-server@5    # запустит инстанс с портом 27015
sudo systemctl status sbox-server@5
sudo journalctl -u sbox-server@5 -f          # живой лог
```

**Плюс шаблона `@`:** один юнит — много инстансов на одной машине (см. [27.03](27_03_Server_Configuration.md)).

### Windows: NSSM

[NSSM](https://nssm.cc/) превращает любой `.exe` в Windows-сервис:

```bat
nssm install SboxServer1 C:\sbox\sbox-server.exe
nssm set     SboxServer1 AppParameters "+game your.gameident +port 27015 +hostname \"My Server\""
nssm set     SboxServer1 AppDirectory   C:\sbox
nssm set     SboxServer1 AppRestartDelay 10000
nssm start   SboxServer1
```

После этого сервер сам поднимется при ребуте Windows.

## 3. Мониторинг

Минимум, который **обязателен**:

| Метрика | Зачем | Откуда |
|---|---|---|
| CPU % | определить перегруз tick-а | `top`/Task Manager |
| RAM | поймать утечку (растёт час за часом) | `top` |
| Net in/out, КБ/с | узкое горлышко канала | `iftop`, `nload` |
| Online | сколько игроков | `Connection.All.Count()` через ConCmd |
| Avg ping | здоровье канала | `Connection.Ping` агрегатом |
| FixedDelta | держится ли tick | `Log.Info(Time.FixedDelta)` |

### Простейший экспортер метрик

```csharp
public sealed class MetricsExporter : Component
{
    [Property] public float Period { get; set; } = 10f;
    private TimeSince _since;

    protected override void OnFixedUpdate()
    {
        if ( !Game.IsServer ) return;
        if ( _since < Period ) return;
        _since = 0;

        var online = Connection.All.Count();
        var avgPing = Connection.All.Average( c => (float)c.Ping );

        Log.Info( $"[Metrics] online={online} avgPing={avgPing:F0}ms" );
        // Дополнительно: Http.RequestAsync на свой Prometheus pushgateway
    }
}
```

Под Linux `journalctl -u sbox-server@5 | grep Metrics` — даст таймсерии для grep/awk.

### Алерты

Минимум 3 алерта:

1. **Сервис лёг** (systemd `Restart` сработал > 3 раз / час).
2. **Online = 0 на ожидаемом сервере 30 минут** (что-то отвалило логин).
3. **CPU > 90% > 5 минут** (перегрузка → tick рвётся → игроков «телепортирует»).

В простом виде — bash-скрипт + cron + Telegram-бот; в проде — Prometheus/Grafana/Alertmanager.

## 4. Обновление

### Обновление движка/сервера s&box

Раз в 1–2 недели:

```bash
# 1. Объяви о рестарте через игру (за 5 минут)
# 2. Останови сервер
sudo systemctl stop sbox-server@5

# 3. Обнови сервер через SteamCMD
steamcmd +login anonymous +app_update 1892930 validate +quit

# 4. Запусти
sudo systemctl start sbox-server@5
```

Под Windows — то же самое через NSSM.

### Обновление кода своего проекта

Два пути:

**А. Hotload в живом проекте.** При локальном `.sbproj` ([27.04](27_04_Local_Project_Serverside_Code.md)) — пересобирается код → клиенты мягко получают новую сборку. Это идеально для микро-патчей, но опасно для больших изменений (см. ниже).

**Б. Перезапуск с миграцией состояния.** Для серьёзных изменений (новые поля у игроков, миграции БД):

1. **Объявление в чате**: «рестарт через 5 минут».
2. Дёрни `[ConCmd.Server] save_all` — все сервисы пишут состояние в БД ([26.12](26_12_Http_Requests.md)).
3. `kick all "Restart"` или просто `systemctl restart`.
4. Сервер подхватывает новый код (новый `.sbproj` или `git pull` + `dotnet build`, если у тебя кастомная сборка).
5. При старте сервисы **читают** состояние из БД.
6. Игроки коннектятся обратно.

> ⚠️ **Hotload не сохраняет всё.** Несериализуемые поля, открытые сокеты, фоновые `Task` — обнулятся. Это особенно бьёт по сервисам типа DB-pool. Для критичных вещей — рестарт.

### Канареечные релизы

На «больших» сетях — поднимай **второй сервер** с новой версией, переключай туда часть игроков. Если 1 час всё ок — катаешь на остальных.

## 5. Бэкапы

Что бэкапить:

| Что | Как часто | Куда |
|---|---|---|
| `users/config.json` | при изменении | git-repo с конфигами (см. ниже) |
| Внешняя БД (Postgres, Redis-AOF) | каждые **15 минут** | S3/B2 + локальная копия |
| Логи сервера (`journalctl`) | rotate ежедневно, хранить 30 дней | `/var/log/` |
| Save-файлы (если используешь [23.02](23_02_SaveSystem.md)) | каждый час + перед рестартом | S3/B2 |
| `.sbproj` и код | git, постоянно | GitHub/Gitea |

**Главное правило:** если бэкап нельзя восстановить из чистой ОС за 1 час — это не бэкап, это «иллюзия».

### Конфиги в git

Заведи отдельный приватный репозиторий `sbox-server-configs` с:

- `users/config.json`
- `server.cfg`
- `systemd/*.service`

Любая правка — через PR, история коммитов = история «кто и когда выдал админку».

## 6. Защита

### От DDoS

s&box идёт через Steam Datagram Relay → DDoS на твой публичный IP **затруднён**, но не невозможен.

- **Не светить настоящий IP**, если игроки коннектятся через Steam-relay.
- Запретить `ICMP echo` на firewall.
- Если хостинг даёт «защищённый IP» — включить.
- Лимит коннектов на один SteamId (если игрок 100 раз в минуту коннектится — тушить).

### От читеров и эксплуатации команд

- **Все** опасные `[ConCmd.Server]` — через `Connection.HasPermission` ([27.05](27_05_User_Permissions.md)).
- Лимит RPC: счётчик «сколько за секунду» на каждого `Connection`. Превысил — temp-ban.
- Ничего критичного через `[Sync]` от клиента — только из-под `SyncFlags.FromHost`.
- Серверный код — в `#if SERVER` ([27.04](27_04_Local_Project_Serverside_Code.md)).

### От спам-ботов в чате

- Минимальный playtime для отправки чата (5 минут).
- Проверка SteamId на «private profile + few games» — флаг бот-аккаунта.
- Mute-команда у модераторов с тем же claim-ом ([27.05](27_05_User_Permissions.md)).

### От падений (паника в коде)

- Логируй любое исключение (см. [00.30](00_30_Log_Assert.md)).
- `Restart=on-failure` в systemd — сам поднимет.
- Алерт на перезапуск > 3 раз в час: что-то реально сломано.

## 7. Чек-лист «сервер готов в прод»

- [ ] Установлен под отдельным non-root пользователем.
- [ ] Запускается через systemd/NSSM, перезапуск автоматический.
- [ ] `users/config.json` — только реальные администраторы.
- [ ] Все опасные ConCmd проверяют `HasPermission`.
- [ ] Логи всех админ-действий пишутся (Audit log из [27.05](27_05_User_Permissions.md)).
- [ ] Внешняя БД, бэкапы каждые 15 минут.
- [ ] Метрики: online, ping, CPU, RAM пишутся регулярно.
- [ ] Алерт «лежит / перегружен / нет игроков долго» работает.
- [ ] Конфиги (`users/config.json`, `server.cfg`) лежат в git.
- [ ] Документ «как поднять с нуля за 1 час» написан в README репозитория.
- [ ] Стресс-тест ботами на пиковую нагрузку проведён ([27.08](27_08_Scaling_Players.md)).
- [ ] Решение «как обновлять без даунтайма» отработано на стейдже.

## Что важно запомнить

- Production-сервер — это **сервис**, а не «процесс, который кто-то руками запустил».
- systemd/NSSM, мониторинг и алерты — **обязательны**, не «потом».
- Бэкапы критичных данных каждые 15 минут — норма для RP/MMO.
- Hotload — для патчей; рестарт — для серьёзных изменений и миграций.
- Все опасные команды и серверный код — за `Connection.HasPermission` и `#if SERVER`.

## Поздравляю — Фаза 27 пройдена!

Теперь у тебя есть полная картина: от установки `sbox-server.exe` до архитектуры MMO. Возвращайся к [27.07](27_07_Networking_Patterns.md) и [27.09](27_09_Genre_Examples.md) как к справочнику, когда будешь делать конкретный жанр.

## Что дальше?

- 📚 [Facepunch/sbox-docs → networking](https://github.com/Facepunch/sbox-docs/tree/master/docs/networking) — продолжай следить за официалкой; раздел dedicated-servers активно растёт.
- 🔁 Перечитай [Фазу 0.5](00_09_GameObject.md) и [Фазу 26](26_01_Physics_Bodies.md) — теперь, с пониманием dedicated, многое заиграет иначе.
- 🛠 Возьми минимальный жанр из [27.09](27_09_Genre_Examples.md) (DM на 16) и доведи до прода — это даст 80% опыта мультиплеер-разработки.

Удачи, и да хранит твой сервер `Restart=on-failure`.
