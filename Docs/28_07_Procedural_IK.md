# 28.07 — Процедурная IK: руки и ноги по миру

## Что мы делаем?

Разбираемся с **Inverse Kinematics** — техникой «дотянись этой косточкой до этой точки в мире, а остальные кости разложи правильно». В Sandbox IK решает две задачи:

1. **Руки** на оружии/пропе (не «висят в воздухе», а лежат на цевье).
2. **Ноги** на неровной поверхности (не проваливаются и не висят над ступенькой).

## Forward vs Inverse Kinematics

| FK (Forward) | IK (Inverse) |
|---|---|
| Задаёшь углы каждого сустава вручную (это делает анимация). | Задаёшь **точку**, куда должна попасть кисть/стопа, а движок сам подкручивает плечо/локоть/запястье. |
| Полностью художник. | Полу-процедурно: цель приходит из C#. |

В s&box IK реализован как **узел в AnimGraph** (IK Chain) и **API на `SkinnedModelRenderer`**:

```csharp
renderer.SetIk(   string chainName, Transform worldTarget );
renderer.ClearIk( string chainName );
```

`chainName` — имя цепочки, которую художник определил в графе (для Citizen — `"hand_left"`, `"hand_right"`, `"foot_left"`, `"foot_right"`).

## Как работает IK Chain

В AnimGraph узел IK Chain настраивается так:

- **Кости цепочки**: например, `upperarm_R → lowerarm_R → hand_R`.
- **Цель** (target): мировая `Transform`. Берётся либо из параметра графа, либо из `SetIk`.
- **Полюс** (pole vector): куда направлять локоть/колено, чтобы цепочка сгибалась естественно.
- **Вес** (0..1): полная IK или плавный микс с FK-позой.

Дальше каждый кадр граф решает 2-bone (или N-bone) IK-задачу: «найди такие углы, чтобы кисть оказалась в `target.Position` с ориентацией `target.Rotation`».

## IK рук на оружии — реальный пример

Из `Code/Npcs/Layers/AnimationLayer.Hold.cs:206`:

```csharp
_renderer.SetIk( "hand_right", new Transform( rightHandPos, rightRot ) );
_renderer.SetIk( "hand_left",  new Transform( leftHandPos,  leftRot  ) );
```

`rightHandPos`/`leftHandPos` — точки на пропе (по бокам или снизу), `rightRot`/`leftRot` — куда «смотрит ладонь». NPC будто реально держит ящик: одна рука слева, другая справа, ладони внутрь.

Очистка обязательна:

```csharp
// AnimationLayer.Hold.cs:130
_renderer.ClearIk( "hand_right" );
_renderer.ClearIk( "hand_left"  );
```

Иначе после `ClearHeldProp` руки продолжали бы тянуться к старой точке.

## IK для оружия в руках игрока

Стандартный паттерн (если делаешь своё оружие через Citizen):

1. На модели оружия художник добавил **attachment-точки** `LeftHand` и `RightHand` — это `Transform`-маркеры на цевье/прикладе.
2. Каждый кадр в коде:
   ```csharp
   var lh = weaponModel.GetAttachment( "LeftHand" );
   var rh = weaponModel.GetAttachment( "RightHand" );
   if ( lh.HasValue ) renderer.SetIk( "hand_left",  lh.Value );
   if ( rh.HasValue ) renderer.SetIk( "hand_right", rh.Value );
   ```
3. При смене оружия / прятании — `ClearIk("hand_left")`, `ClearIk("hand_right")`.

В Sandbox этим занимается стандартный механизм Citizen + `holdtype` (см. 28.04). Голую IK ты пишешь только когда стандартного hold-set’а недостаточно — например, для произвольной пропы (28.05) или необычного оружия с уникальным хватом.

## IK ног — адаптация к рельефу

Идея: на ступеньке стопа, которая выше, должна стоять выше. Алгоритм каждого кадра:

```csharp
// псевдокод
foreach ( var foot in new[] { ("foot_left", leftFootBone), ("foot_right", rightFootBone) } )
{
    var bonePos = foot.Item2.WorldPosition;
    var tr = Scene.Trace
        .Ray( bonePos + Vector3.Up * 16f, bonePos + Vector3.Down * 32f )
        .Run();

    if ( tr.Hit )
    {
        var targetRot = Rotation.LookAt( renderer.WorldRotation.Forward, tr.Normal );
        renderer.SetIk( foot.Item1, new Transform( tr.EndPosition, targetRot ) );
    }
    else
    {
        renderer.ClearIk( foot.Item1 );
    }
}
```

В Citizen для этого есть встроенные параметры `CitizenAnimationHelper.IkLeftFoot` / `IkRightFoot` — присваиваешь `Transform?`, и хелпер сам делает `SetIk/ClearIk`.

## Pole vector — куда сгибать локоть/колено

Без pole-вектора 2-bone IK имеет бесконечное число решений (рука может «вывернуться» наизнанку). Художник в графе указывает **направление полюса**:

- Для рук — обычно «локоть наружу-вниз», задаётся вектором за плечом.
- Для ног — «колено вперёд», задаётся точкой перед щиколоткой.

Если IK ломается («рука выворачивается за спину») — почти всегда виноват pole в графе.

## Веса и плавность

`SetIk` ставит вес `1.0` (полная IK) сразу. Если нужно плавное включение — обычно добавляют пар-параметр в графе `ik_left_weight` (float 0..1), а узел IK Chain домножает на него:

```csharp
ikWeight = ikWeight.LerpTo( 1f, Time.Delta * 4f );
renderer.Set( "ik_left_weight", ikWeight );
renderer.SetIk( "hand_left", target );
```

Без этого при подхвате пропы рука «телепортируется» в нужную точку — некрасиво.

## Ограничения и подводные камни

- **IK сжирает кадр**, если делать по 10 раз. Достаточно одного `SetIk` на цепочку в кадр.
- **Цель должна быть достижима.** Если положить руку в 3 метрах от плеча — рука вытянется в струну, локоть распрямится «в обратную сторону».
- **Не комбинируй IK и физический рэгдолл** на одном скелете — будет конфликт.
- **`ClearIk` обязателен** — иначе цепочка продолжает тянуться к старому таргету и в неактивном состоянии.

## Подсказки на практике

- Для **Citizen + оружие** — пользуйся `holdtype` и attachment’ами, IK вручную не нужен.
- Для **Citizen + произвольная пропа** — смотри `AnimationLayer.Hold.cs` (28.05).
- Для **ног на рельефе** — `CitizenAnimationHelper.IkLeftFoot/IkRightFoot` каждый кадр.
- Для **робота / не-Citizen скелета** — собирай IK Chain’ы в своём графе руками.

## Результат

После этого этапа ты знаешь:

- ✅ Что такое IK и чем отличается от FK.
- ✅ Где в графе настраиваются цепочки и pole-векторы.
- ✅ API `SetIk(name, transform)` / `ClearIk(name)` и стандартные имена цепочек.
- ✅ Два классических применения: руки на оружии/пропе, ноги на рельефе.
- ✅ Как сделать плавное включение IK через дополнительный вес-параметр.

---

📚 **Официальная документация Facepunch:** [animation/ik.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.08 — События анимаций (anim events / tags)](28_08_Animation_Events.md)
