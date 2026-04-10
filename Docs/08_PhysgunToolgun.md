# 08 — Physgun, Toolgun и инструменты 🔧

## Обзор

**Physgun** и **Toolgun** — два «инструментальных» оружия, определяющих геймплей Sandbox. Physgun позволяет хватать и перемещать объекты, а Toolgun — применять различные инструменты (Weld, Rope, Remover и т.д.).

---

## 08.01 — Physgun (физическая пушка)

### Файлы
- `Code/Weapons/PhysGun/Physgun.cs` — основная логика
- `Code/Weapons/PhysGun/Physgun.Effects.cs` — лучи и визуал
- `Code/Weapons/PhysGun/Physgun.Hud.cs` — HUD (прицел)
- `Code/Weapons/PhysGun/PhygunViewmodel.cs` / `PhygunWorldmodel.cs` — модели

### GrabState — состояние захвата

```csharp
public struct GrabState
{
    bool Active;           // захвачен ли объект
    bool Pulling;          // тянем к себе (ПКМ)
    GameObject GameObject; // захваченный объект
    Vector3 LocalOffset;   // точка захвата в локальных координатах
    Vector3 LocalNormal;   // нормаль в точке захвата
    Rotation GrabOffset;   // вращение (при зажатом E)
    float GrabDistance;     // расстояние до объекта
}
```

`[Sync]` — синхронизируется по сети, все видят луч.

### Управление

| Кнопка | Действие |
|--------|---------|
| **ЛКМ (зажать)** | Захватить объект и удерживать |
| **ЛКМ (отпустить)** | Отпустить объект |
| **ПКМ (зажать)** | Притянуть объект к себе |
| **ПКМ (при захвате)** | Заморозить объект + отпустить |
| **ЛКМ (при притягивании)** | Запустить объект вперёд (launch) |
| **E (при захвате)** | Режим вращения (мышь вращает объект, не камеру) |
| **Shift (при E)** | Привязка к сетке 45° |
| **Alt (при E)** | Привязка к сетке 15° |
| **Колесо мыши** | Изменить дистанцию захвата |
| **R** | Разморозить все связанные объекты |

### Физика захвата

На хосте (`OnFixedUpdate`):
```csharp
var direction = (target.Position - rb.SmoothPhysicsPosition);
rb.Velocity = direction * 10f;    // плавное перемещение
rb.AngularVelocity = 0;           // не вращаться
```

Объект следует за прицелом с плавным ускорением. При `Pulling` (притягивание) — постоянная сила к игроку.

### IPhysgunEvent — фильтрация

Перед захватом проверяется `IPhysgunEvent`:
```csharp
var e = new IPhysgunEvent.GrabEvent { Grabber = connection };
IPhysgunEvent.PostToGameObject( go, x => x.OnPhysgunGrab( e ) );
if ( e.Cancelled ) return; // отказ
```

`Ownable` использует это для prop protection.

---

## 08.02 — Toolgun (инструментальная пушка)

### Файлы
- `Code/Weapons/ToolGun/Toolgun.cs` — основная логика
- `Code/Weapons/ToolGun/Toolgun.Effects.cs` — визуальные эффекты
- `Code/Weapons/ToolGun/Toolgun.Screen.cs` — экран на viewmodel'е
- `Code/Weapons/ToolGun/ToolMode.cs` — базовый класс инструмента
- `Code/Weapons/ToolGun/ToolMode.*.cs` — расширения ToolMode
- `Code/Weapons/ToolGun/Modes/*.cs` — конкретные инструменты

### Архитектура

```
Toolgun : ScreenWeapon
  └── Sync: string ToolModeName
       └── ToolMode (дочерний компонент)
            ├── Weld
            ├── Rope
            ├── BallSocket
            ├── Elastic
            ├── Remover
            ├── Balloon
            ├── Thruster
            ├── Wheel
            ├── Hoverball
            ├── Duplicator
            ├── ...
```

### Переключение инструмента

```csharp
[Sync( SyncFlags.FromHost ), Change]
public string ToolModeName { get; set; }
```

При изменении:
1. Уничтожается текущий `ToolMode`
2. Ищется тип по имени через `TypeLibrary`
3. Создаётся новый компонент `ToolMode`
4. Экран viewmodel'а обновляется

### ToolMode — базовый класс инструмента

```csharp
public abstract class ToolMode : Component
{
    public Player Owner { get; }
    public Toolgun Toolgun { get; }

    // Вызывается при ЛКМ
    public virtual bool Primary( SceneTraceResult tr ) => false;

    // Вызывается при ПКМ
    public virtual bool Secondary( SceneTraceResult tr ) => false;

    // Вызывается при R
    public virtual bool Reload( SceneTraceResult tr ) => false;

    // Рисует HUD-подсказки
    public virtual void DrawGizmos( SceneTraceResult tr ) { }
}
```

### ToolMode.Helpers — вспомогательные методы

```csharp
// Проверяет IToolgunEvent (prop protection)
protected bool CanUseToolOn( GameObject go ) { ... }

// Находит или добавляет Rigidbody
protected Rigidbody FindOrAddRigidbody( GameObject go ) { ... }

// Создаёт запись в Undo
protected UndoSystem.Entry CreateUndo( string name ) { ... }
```

### ToolMode.Cookies — сохранение настроек

```csharp
// Сохраняет настройку инструмента между сессиями
protected T GetCookie<T>( string name, T defaultValue ) { ... }
protected void SetCookie<T>( string name, T value ) { ... }
```

### ToolMode.SnapGrid — привязка к сетке

```csharp
// Привязывает позицию к сетке для точного позиционирования
protected Vector3 Snap( Vector3 position ) { ... }
```

---

## 08.03 — Конкретные инструменты

### Weld — сварка

Соединяет два объекта `FixedJoint`. ЛКМ по первому → ЛКМ по второму → сварены.

### Rope — верёвка

Создаёт пружинный `Joint` между двумя объектами + визуальную верёвку (`LineRenderer`). Настройки: длина, жёсткость.

### Elastic — эластик

Как Rope, но с настраиваемой пружинной жёсткостью и демпфированием.

### BallSocket — шаровой шарнир

Создаёт `BallSocketJoint` — объект может вращаться вокруг точки крепления (как маятник).

### Slider — ползунок

`SliderJoint` — объект скользит по оси.

### Hydraulic — гидравлика

Создаёт `HydraulicEntity` — контролируемое соединение, которое расширяется/сжимается по нажатию клавиш.

### Remover — удаление

ЛКМ → удаляет объект. R → удаляет все связанные объекты (через `LinkedGameObjectBuilder`).

### Balloon — воздушный шар

Спавнит `BalloonEntity` — объект с антигравитацией, поднимает привязанный объект вверх. Цвет настраивается.

### Thruster — двигатель

Спавнит `ThrusterEntity` — приложение постоянной силы в направлении. Включается/выключается клавишей.

### Wheel — колесо

Спавнит `WheelEntity` — вращающееся колесо с мотором. Управление клавишами вперёд/назад.

### Hoverball — парящий шар

Спавнит `HoverballEntity` — объект, который удерживается на заданной высоте.

### NoCollide — отключение коллизий

Помечает два объекта так, что они не сталкиваются друг с другом.

### Resizer — изменение размера

Колесом мыши меняет масштаб объекта.

### Mass — масса

Меняет массу Rigidbody. Используется `MassOverride` компонент для сохранения при дупликации.

### Trail — след

Добавляет визуальный след (`LineRenderer`) к объекту.

### Unbreakable — неразрушимость

Делает проп неразрушимым (или возвращает разрушимость).

### Decal — декаль

Размещает текстуру на поверхности.

### Emitter — излучатель

Спавнит частицы/пропы из точки.

### Linker — линковщик

Создаёт `ManualLink` между объектами (для дупликации).

### Duplicator — дупликатор

Сложный инструмент: сохраняет/загружает конструкции (группы объектов с соединениями). Использует `DuplicationData` для сериализации и `LinkedGameObjectBuilder` для поиска связей.

---

## Проверка

### Physgun
1. ЛКМ по пропу → захвачен, следит за прицелом
2. ПКМ → притягивание
3. E + мышь → вращение
4. Колесо → изменение дистанции
5. ПКМ при захвате → заморозка

### Toolgun
1. Переключи на Weld → ЛКМ по двум пропам → сварены
2. Rope → создай верёвку между двумя пропами
3. Remover → удали проп
4. Balloon → создай шарик, поднимающий проп

## 🎉 Фаза 8 завершена!
