# 06 — Система оружия (Weapon System) 🔫

## Обзор

Вся система оружия построена на иерархии наследования:

```
Component
  └── BaseCarryable          — любой переносимый предмет (камера, фонарик)
        ├── BaseWeapon        — оружие с патронами, перезарядкой, атакой
        │     ├── BaseBulletWeapon  — стреляющее оружие (пули, трейс, отдача)
        │     │     └── IronSightsWeapon  — оружие с прицеливанием (ADS)
        │     └── (другое оружие)
        └── MeleeWeapon       — ближний бой (трейс ближнего радиуса)

WeaponModel (абстрактный)
  ├── ViewModel  — модель от 1-го лица (прикреплена к камере)
  └── WorldModel — модель 3-го лица (прикреплена к руке персонажа)
```

---

## 06.01 — BaseCarryable (базовый переносимый предмет)

### Файлы
- `Code/Game/Weapon/BaseCarryable/BaseCarryable.cs` — основная логика
- `Code/Game/Weapon/BaseCarryable/BaseCarryable.ViewModel.cs` — создание/удаление viewmodel
- `Code/Game/Weapon/BaseCarryable/BaseCarryable.WorldModel.cs` — создание/удаление worldmodel

### Ключевые свойства

| Свойство | Тип | Назначение |
|----------|-----|-----------|
| `DisplayName` | `string` | Имя в UI |
| `DisplayIcon` | `Texture` | Иконка в инвентаре |
| `ItemPrefab` | `GameObject` | Префаб для выброса |
| `Value` | `int` | Приоритет для автопереключения |
| `InventorySlot` | `int` | Номер слота (sync) |
| `HoldType` | `HoldTypes` | Тип анимации удержания |
| `ViewModelPrefab` | `GameObject` | Модель 1-го лица |
| `WorldModelPrefab` | `GameObject` | Модель 3-го лица |

### TraceAttackInfo — структура атаки

```csharp
public record struct TraceAttackInfo( 
    GameObject Target, float Damage, TagSet Tags, Vector3 Position, Vector3 Origin )
```

Универсальная структура для передачи информации об атаке. Создаётся из `SceneTraceResult`:

```csharp
var info = TraceAttackInfo.From( tr, damage, tags );
```

### TraceAttack — RPC атаки

```csharp
[Rpc.Host]
public void TraceAttack( TraceAttackInfo attack )
```

Клиент отправляет на хост: «я попал по X с уроном Y». Хост проверяет и применяет урон. Это **серверная авторизация** — клиент не может напрямую наносить урон.

### ViewModel (модель от 1-го лица)

```csharp
protected void CreateViewModel()
{
    ViewModel = ViewModelPrefab.Clone( ... );
    ViewModel.Tags.Add( "firstperson", "viewmodel" );
    // Тег "firstperson" — камера исключает его из рендера 3-го лица
}
```

ViewModel создаётся **только для локального игрока** (`player.IsLocalPlayer`). Другие игроки видят только WorldModel.

### WorldModel (модель 3-го лица)

```csharp
public void CreateWorldModel( SkinnedModelRenderer renderer )
{
    worldModel = WorldModelPrefab.Clone( ... );
    worldModel.Parent = renderer.GetBoneObject( ParentBone );
    // Привязана к кости "hold_r" (правая рука)
}
```

### SetDropped — переключение состояния

```csharp
public void SetDropped( bool dropped )
{
    rb.Enabled = dropped;       // физика
    col.Enabled = dropped;      // коллайдер
    droppedWeapon.Enabled = dropped;  // скрипт подбора
}
```

Когда оружие в руках — физика выключена. Когда выброшено — включена.

---

## 06.02 — BaseWeapon (базовое оружие)

### Файлы
- `Code/Game/Weapon/BaseWeapon/BaseWeapon.cs` — логика стрельбы
- `Code/Game/Weapon/BaseWeapon/BaseWeapon.Ammo.cs` — система патронов
- `Code/Game/Weapon/BaseWeapon/BaseWeapon.Reloading.cs` — перезарядка

### Цикл стрельбы

```
OnControl(player)
  → CanPrimaryAttack()?    // проверки: есть патроны? не перезаряжается? кулдаун прошёл?
    → WantsPrimaryAttack()? // Input.Down("attack1")?
      → PrimaryAttack()     // виртуальный метод — переопределяется в наследниках
```

### TimeUntilNextShotAllowed

```csharp
protected TimeUntil TimeUntilNextShotAllowed;

public void AddShootDelay( float seconds )
{
    TimeUntilNextShotAllowed = seconds;
}
```

`TimeUntil` — автоматический обратный отсчёт. `TimeUntilNextShotAllowed > 0` → ещё нельзя стрелять.

### Система патронов (Ammo)

Два режима:
1. **С обоймами** (`UsesClips = true`): ClipContents (текущие) + ReserveAmmo (запас)
2. **Без обойм** (`UsesClips = false`): только ReserveAmmo

```csharp
public bool TakeAmmo( int count )
{
    if ( UsesClips )
    {
        if ( ClipContents < count ) return false;
        ClipContents -= count;
        return true;
    }
    // Без обойм — берём из резерва
    if ( ReserveAmmo < count ) return false;
    ReserveAmmo -= count;
    return true;
}
```

### Перезарядка (Reloading)

Async-перезарядка с поддержкой отмены:

```csharp
public virtual async void OnReloadStart()
{
    var cts = new CancellationTokenSource();
    reloadToken = cts;
    isReloading = true;

    try
    {
        await ReloadAsync( cts.Token );
    }
    finally
    {
        isReloading = false;
    }
}
```

**Инкрементальная перезарядка** (`IncrementalReloading = true`): по 1 патрону за цикл (как в дробовике). Можно прервать на любом патроне.

### DryFire и TryAutoReload

- `DryFire()` — звук «клик» пустого оружия (для оружия без перезарядки)
- `TryAutoReload()` — звук «клик» + начать перезарядку (если можно)

### IPlayerControllable

```csharp
public interface IPlayerControllable
{
    void OnStartControl();
    void OnEndControl();
    void OnControl();
}
```

Оружие может управляться из транспорта (сиденья). `ShootInput`/`SecondaryInput` — кнопки для стрельбы из турели.

---

## 06.03 — BaseBulletWeapon (стреляющее оружие)

### BulletConfiguration

```csharp
public record struct BulletConfiguration
{
    float Damage;                // урон за пулю
    float BulletRadius;          // радиус трейса
    Vector2 AimConeBase;         // базовый разброс
    Vector2 AimConeSpread;       // доп. разброс при стрельбе
    float AimConeRecovery;       // время восстановления точности
    Vector2 RecoilPitch;         // отдача (вертикально)
    Vector2 RecoilYaw;           // отдача (горизонтально)
    float CameraRecoilStrength;  // сила визуальной отдачи
    float CameraRecoilFrequency; // частота тряски
    float Range;                 // дальность (юниты)
}
```

### ShootBullet — ядро стрельбы

```csharp
protected void ShootBullet( float fireRate, in BulletConfiguration config )
```

1. Проверки: есть патроны? перезаряжается? кулдаун?
2. `TakeAmmo(1)` — забираем 1 патрон
3. `AddShootDelay(fireRate)` — следующий выстрел через X сек
4. **Aim Cone** — добавляем разброс к направлению:
   ```csharp
   forward = Owner.EyeTransform.Rotation.Forward
       .WithAimCone( base.x + spread * cone.x, base.y + spread * cone.y );
   ```
5. **Трейс** — луч из глаз с `Radius` и `UseHitboxes()`
6. **ShootEffects** — RPC на всех клиентах (звук, вспышка, гильза, импакт)
7. **TraceAttack** — RPC на хосте (урон)
8. **Отдача** — смещаем EyeAngles + CameraNoise.Recoil

### Standalone режим (без игрока)

Оружие может стрелять без владельца (например, турель):
- Стреляет из `MuzzleTransform` вместо глаз игрока
- Нет aim cone / отдачи
- `ShootForce` толкает оружие назад (физическая отдача)

### ShootEffects

```csharp
[Rpc.Broadcast]
public void ShootEffects( ... )
```

На всех клиентах:
1. Анимация «b_attack» на модели
2. ViewModel: вспышка дула, гильза, трейсер
3. Звук выстрела (без спатиализации для стреляющего)
4. Импакт на поверхности (искры, дырка, кровь)

---

## 06.04 — IronSightsWeapon (прицеливание)

Наследует `BaseBulletWeapon` и добавляет **прицеливание через прицел** (ADS):

```csharp
public abstract class IronSightsWeapon : BaseBulletWeapon
{
    [Property] public float IronSightsFireScale { get; set; } = 0.2f;

    private bool _isAiming;

    public override void OnControl( Player player )
    {
        base.OnControl( player );

        _isAiming = Input.Down( "attack2" );
        // Уведомляем ViewModel об изменении
    }

    protected BulletConfiguration GetBullet()
    {
        if ( !_isAiming ) return Bullet;

        var config = Bullet;
        config.AimConeBase *= IronSightsFireScale;    // разброс уменьшается
        config.AimConeSpread *= IronSightsFireScale;  // при прицеливании
        return config;
    }
}
```

Когда прицеливаешься — разброс × 0.2 (в 5 раз точнее). Прицел скрывается, ViewModel показывает анимацию ADS.

---

## 06.05 — MeleeWeapon (ближний бой)

Наследует `BaseCarryable` (не `BaseWeapon`!) — нет патронов, нет перезарядки.

### Swing — удар

```csharp
public void Swing( Player player )
{
    var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay, Range )
        .Radius( SwingRadius )
        .UseHitboxes()
        .Run();

    timeUntilSwing = tr.Hit ? SwingDelay : MissSwingDelay;

    SwingEffects( ... );
    TraceAttack( TraceAttackInfo.From( tr, Damage ) );
}
```

- **Попал** → кулдаун `SwingDelay` (0.5с)
- **Промахнулся** → кулдаун `MissSwingDelay` (0.75с) — наказание за промах
- Camera Punch + Shake для ощущения удара

---

## 06.06 — WeaponModel, ViewModel, WorldModel

### WeaponModel (абстрактный базовый)

```csharp
public abstract class WeaponModel : Component
{
    SkinnedModelRenderer Renderer;
    GameObject MuzzleTransform;   // точка вспышки
    GameObject EjectTransform;    // точка вылета гильз
    GameObject MuzzleEffect;      // префаб вспышки
    GameObject EjectBrass;        // префаб гильзы
    GameObject TracerEffect;      // префаб трейсера
}
```

Методы: `Deploy()`, `DoTracerEffect()`, `DoEjectBrass()`, `DoMuzzleEffect()`.

### ViewModel (от 1-го лица)

Расширяет `WeaponModel`:
- **Инерция** — оружие «отстаёт» от взгляда при повороте
- **ICameraSetup** — позиционирует себя на камере каждый кадр
- **Анимация** — передаёт параметры в AnimGraph: скорость, направление, перезарядка
- **Throwables** — поддержка анимации метания гранат

### WorldModel (от 3-го лица)

Минимальная обёртка: `OnAttack()` и `CreateRangedEffects()` (трейсер создаётся только если нет ViewModel).

---

## Проверка всей системы

1. Возьми пистолет → видишь ViewModel от 1-го лица
2. Переключись в 3-е лицо → видишь WorldModel в руке
3. Стреляй → вспышка, звук, гильза, трейсер, импакт
4. Опустошни обойму → звук «клик», автоперезарядка
5. Нажми R → ручная перезарядка с анимацией
6. Прицелься (ПКМ на IronSightsWeapon) → разброс уменьшается
7. Возьми нож → ближний бой, camera shake при ударе

## 🎉 Фаза 6 завершена!

Система оружия документирована. Следующая фаза — **Фаза 7: Конкретное оружие и инструменты**.
