# 28.06 — Анимации view-модели оружия (ViewModel.cs)

## Что мы делаем?

Разбираем `Code/Game/Weapon/WeaponModel/ViewModel.cs` — анимационный мост между **игроком/камерой** и **моделью оружия от первого лица**. Тут впервые появляются bob/sway, инерция, deploy/reload/attack — стандартный «джентльменский набор» FPS.

## Что такое view-модель

**View-модель** (она же «руки + оружие в правом нижнем углу экрана») — отдельная модель, которая:

- Видна **только локальному игроку**, чьё это оружие.
- Привязана к камере, а не к миру.
- Имеет **собственный AnimGraph** с параметрами оружия (move_x, aim_pitch, b_attack, …).
- Двигается за счёт инерции/bob’а — это создаёт ощущение «оружие в руках».

`ViewModel : WeaponModel, ICameraSetup` — и есть этот компонент. Он живёт на дочернем GameObject view-модели, имеет ссылку на `Renderer` (это `SkinnedModelRenderer`).

## Параметры графа view-модели

### Базовые «состояния»

```csharp
Renderer.Set( "b_twohanded", true );                       // двуручное удержание
Renderer.Set( "deploy_type", UseFastAnimations ? 1 : 0 );  // выбор клипа доставания
Renderer.Set( "reload_type", UseFastAnimations ? 1 : 0 );  // выбор клипа перезарядки
Renderer.Set( "b_grounded",  playerController.IsOnGround );
```

`UseFastAnimations` — `[Property]`-флаг компонента. Художник ставит «галочку» в инспекторе под конкретный пистолет, и в графе включается «быстрый» вариант клипов.

### Bob — болтанка от ходьбы

```csharp
Renderer.Set( "move_bob",
    GamePreferences.ViewBobbing
        ? playerController.Velocity.Length.Remap( 0, playerController.RunSpeed * 2f )
        : 0 );
```

- При ходьбе модуль скорости преобразуется в `0..1` (через `Remap`).
- Если игрок выключил `ViewBobbing` в настройках — `move_bob = 0`, болтанки нет.
- В графе этот float крутит синусоидальное смещение оружия (готовый узел в Citizen-графе для view-моделей).

### Aim + инерция (sway)

```csharp
Renderer.Set( "aim_pitch",         rot.pitch );
Renderer.Set( "aim_pitch_inertia", currentInertia.x * InertiaScale.x );
Renderer.Set( "aim_yaw",           rot.yaw   );
Renderer.Set( "aim_yaw_inertia",   currentInertia.y * InertiaScale.y );
```

`aim_pitch/aim_yaw` — куда смотрит камера. `*_inertia` — **разница** угла между прошлым и текущим кадром (как быстро ты крутишь мышью). Это даёт классический «sway»: резко крутанул мышью — ствол отстаёт и догоняет.

`ApplyInertia()` считает это так:

```csharp
currentInertia = new Vector2(
    Angles.NormalizeAngle( newPitch - lastInertia.x ),
    Angles.NormalizeAngle( lastInertia.y - newYaw ) );
lastInertia = new( newPitch, newYaw );
```

`InertiaScale` (`Vector2`, `[Property]`) — множитель, который художник крутит для конкретного оружия: тяжёлая снайперка отстаёт сильнее, лёгкий пистолет — почти нет.

### Attack

```csharp
Renderer.Set( "attack_hold",
    IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );
...
public override void OnAttack()
{
    Renderer?.Set( "b_attack", true );
    DoMuzzleEffect();
    DoEjectBrass();

    if ( IsThrowable )
    {
        Renderer?.Set( "b_throw", true );
        Invoke( 0.5f, () =>
        {
            Renderer?.Set( "b_deploy_new", true );
            Renderer?.Set( "b_pull",       false );
        } );
    }
}
```

- **`b_attack`** — триггер выстрела (см. 28.03).
- **`attack_hold`** — float `0..1`, длительность зажатой кнопки. Используется графом для держания позы между выстрелами автомата (без него каждый клик играл бы полную анимацию заново).
- **`b_throw` / `b_deploy_new` / `b_pull`** — для метательного оружия (граната): «бросил → достать новую → не подтягивать».

### Reload

```csharp
public void OnReloadStart()
{
    _reloadFinishing = false;
    Renderer?.Set( "speed_reload", AnimationSpeed );
    Renderer?.Set( IsIncremental ? "b_reloading" : "b_reload", true );
}

public void OnIncrementalReload()
{
    Renderer?.Set( "speed_reload", IncrementalAnimationSpeed );
    Renderer?.Set( "b_reloading_shell", true );
}

public void OnReloadFinish()
{
    if ( IsIncremental ) { _reloadFinishing = true; _reloadFinishTimer = 0; }
    else                   Renderer?.Set( "b_reload", false );
}
```

Два варианта:

- **Магазинная** перезарядка (пистолет, винтовка): один клип. `b_reload = true` → играем → `b_reload = false`.
- **Инкрементальная** (дробовик): отдельный клип «всунуть один патрон» (`b_reloading_shell` — триггер) повторяется N раз. Параметр `speed_reload` отдельно для входа/выхода и отдельно для одного «снаряд-в-магазин».

`_reloadFinishing` + `_reloadFinishTimer` — отложенный сброс `b_reloading` через 0.5 сек после OnReloadFinish, чтобы дать графу плавно выйти.

### Move-параметры

```csharp
Renderer.Set( "move_direction",   angle );
Renderer.Set( "move_speed",       velocity.Length );
Renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
Renderer.Set( "move_y", sideward );
Renderer.Set( "move_x", forward  );
Renderer.Set( "move_z", velocity.z );
```

`forward`/`sideward` — проекция мировой скорости на оси **камеры**, не на оси игрока. В view-моделях так логичнее: «смотри-стрейфься влево» = `move_y < 0` независимо от того, как повернут персонаж.

## Цикл жизни

```
OnStart      → отключить тени view-модели (только своя камера должна её видеть)
OnUpdate     → ApplyInertia() → UpdateAnimation()  // каждый кадр
OnAttack     → b_attack, эффекты, для гранаты — b_throw/b_deploy
OnReloadStart/OnIncrementalReload/OnReloadFinish — по событиям BaseWeapon
```

`OnStart` устанавливает теням `RenderType = Off` для всех `ModelRenderer`-ов внутри view-модели — иначе под локальным игроком висит «вторая тень» от рук.

## Подсказки на практике

- **Параметры пиши каждый кадр.** Если положить `move_x` один раз — оружие так и зависнет в этой позе.
- **Триггеры — в обработчиках событий.** `b_attack` — внутри `OnAttack`, не в `OnUpdate`.
- **Bob выключай по `GamePreferences.ViewBobbing`** — у некоторых игроков от него укачивает. Sandbox это уже делает.
- **`UseFastAnimations`** — простой паттерн для двух сетов клипов в одном графе.
- **Не путай `move_x/move_y` view-модели и тела.** В теле — оси игрока, в view-модели — оси камеры.

## Результат

После этого этапа ты знаешь:

- ✅ Какие параметры есть у view-модели и зачем каждый.
- ✅ Что такое sway и как он считается через инерцию углов.
- ✅ Разницу между магазинной и инкрементальной перезарядкой.
- ✅ Как устроен AnimGraph оружия в Sandbox изнутри.

---

📚 **Официальная документация Facepunch:** [animation/index.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.07 — Процедурная IK](28_07_Procedural_IK.md)
