# 07 — Конкретное оружие (Weapons) ⚔️

## Обзор

Все конкретные виды оружия наследуют от базовых классов (Фаза 6) и минимально переопределяют поведение. Файлы находятся в `Code/Weapons/`.

## Иерархия конкретного оружия

```
IronSightsWeapon (прицеливание)
  ├── Colt1911Weapon      — пистолет, полуавтоматический
  ├── GlockWeapon         — пистолет, полуавтоматический
  ├── M4a1Weapon          — штурмовая винтовка, автоматическая
  ├── Mp5Weapon           — пистолет-пулемёт, автоматический
  └── ShotgunWeapon       — дробовик, 8 пелетов за выстрел

BaseBulletWeapon (без прицеливания)
  └── SniperWeapon        — снайперская винтовка с оптикой

MeleeWeapon (ближний бой)
  └── CrowbarWeapon       — лом (пустой класс!)

BaseWeapon (специальное)
  ├── CameraWeapon        — фотокамера с DOF
  ├── HandGrenadeWeapon   — ручная граната
  └── Physgun             — физическая пушка (gmod-стиль)

BaseCarryable (инструменты)
  ├── Toolgun             — инструментальная пушка
  └── ScreenWeapon        — база для оружия с экраном (Toolgun наследует)
```

---

## 07.01 — Огнестрельное оружие

### Паттерн реализации

Каждое огнестрельное оружие:
1. Наследует `IronSightsWeapon` или `BaseBulletWeapon`
2. Переопределяет `PrimaryAttack()` → вызывает `ShootBullet(fireRate)`
3. Переопределяет `DrawCrosshair()` → рисует свой прицел
4. Иногда переопределяет `WantsPrimaryAttack()`:
   - Полуавтомат: `Input.Pressed("attack1")` — один выстрел за нажатие
   - Автомат: `Input.Down("attack1")` (по умолчанию) — стреляет пока зажато

### Colt1911 / Glock (полуавтоматические пистолеты)

```csharp
public class Colt1911Weapon : IronSightsWeapon
{
    [Property] public float PrimaryFireRate { get; set; } = 0.2f;

    protected override bool WantsPrimaryAttack() => Input.Pressed( "attack1" );

    public override void PrimaryAttack()
    {
        ShootBullet( PrimaryFireRate, GetBullet() );
    }
}
```

- `Input.Pressed` — полуавтомат (один выстрел за клик)
- `GetBullet()` — возвращает конфиг пули с учётом прицеливания (IronSights уменьшает разброс)
- Прицел: маленький круг (5px чёрный + 3px белый/красный)

### M4A1 / MP5 (автоматическое оружие)

```csharp
public class M4a1Weapon : IronSightsWeapon
{
    [Property] public float TimeBetweenShots { get; set; } = 0.1f;

    public override void PrimaryAttack()
    {
        ShootBullet( TimeBetweenShots, GetBullet() );
    }
}
```

- `Input.Down` (по умолчанию) — автоматический огонь
- Прицел: крестик с динамическим разбросом (`GetAimConeAmount() * 32`)

### Shotgun (дробовик)

Уникальный: стреляет **8 пелетов** за выстрел, каждый со своим трейсом.

```csharp
for ( int i = 0; i < PelletCount; i++ )
{
    var forward = eyeForward.WithAimCone( ... );
    var tr = Scene.Trace.Ray( ... );
    ShootEffects( ... );
    TraceAttack( ... );
}
```

Инкрементальная перезарядка: по 1 патрону, можно прервать.

### Sniper (снайперская винтовка)

Отличия от обычного оружия:
- ПКМ → прицел (scope) с уменьшенным FOV (20°)
- Чувствительность мыши снижается при прицеливании
- Эффект `SniperScopeEffect` на камере
- Одиночный выстрел, длинная перезарядка

---

## 07.02 — CrowbarWeapon (лом)

```csharp
public class CrowbarWeapon : MeleeWeapon { }
```

Полностью пустой класс! Вся логика в `MeleeWeapon` (Фаза 6). Параметры (урон, дальность, кулдаун) задаются в инспекторе через `[Property]`.

---

## 07.03 — CameraWeapon (фотокамера)

Специальное «оружие» для фотографии:
- Скрывает HUD (`WantsHideHud = true`)
- Управляет FOV и roll камеры через мышь/колесо
- Применяет Depth of Field эффект
- ЛКМ = сделать фото (скриншот)
- ПКМ = авто-фокусировка на объекте
- Слот инвентаря 8 (отдельно от оружия)

---

## 07.04 — HandGrenadeWeapon (ручная граната)

Оружие с «готовкой» (cooking):
1. **ЛКМ зажата** → начинается отсчёт фитиля
2. **ЛКМ отпущена** → граната брошена
3. **Если не бросить за `Lifetime` секунд** → взрыв в руках
4. **ПКМ** → слабый бросок (ближняя дистанция)

Граната — отдельный `TimedExplosive` компонент:
- Физика: Rigidbody + сфера коллизии
- Взрыв через `Lifetime` секунд с момента «приготовления»
- Урон по радиусу, уменьшается с расстоянием

---

## 07.05 — ScreenWeapon (база для экрана)

Абстрактная база для оружия с «экраном» на модели (Toolgun использует):
- Создаёт `RenderTexture` для экрана viewmodel'а
- `DrawScreen()` — виртуальный метод для отрисовки
- `SpinCoil()` — анимация вращающейся катушки на viewmodel'е

---

## Проверка

1. Каждое оружие спавнится через инвентарь
2. Стреляет с правильной скоростью
3. Прицеливание уменьшает разброс (для IronSights)
4. Перезарядка работает (R или автоматическая)
5. Граната: готовка → бросок → взрыв
6. Камера: FOV, DOF, скриншот

## 🎉 Фаза 7 завершена!
