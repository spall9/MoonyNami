using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;

namespace MoonyNami_EB
{
    class Nami
    {
        private Menu menu;

        readonly Spell.Skillshot Q = new Spell.Skillshot(SpellSlot.Q, 875,
            SkillShotType.Circular, 1, int.MaxValue, 150);
        readonly Spell.Targeted W = new Spell.Targeted(SpellSlot.W, 725);
        readonly Spell.Targeted E = new Spell.Targeted(SpellSlot.E, 800);

        readonly Spell.Skillshot R = new Spell.Skillshot(SpellSlot.R, 2750, SkillShotType.Linear, 250, 500, 160);

        private AIHeroClient me = ObjectManager.Player;

        public Nami()
        {
            if (me.ChampionName != "Nami")
                return;

            menu = MainMenu.AddMenu("Nami", "MoonyNami");

            menu.AddLabel("Combo");
            menu.Add("QCombo", new CheckBox("Use Q"));
            menu.Add("WCombo", new CheckBox("Smart W", false));
            menu.Add("WCombo2", new CheckBox("Super Smart W"));
            menu.AddLabel("Calculations are based on algorithms");
            menu.AddSeparator(10);
            menu.Add("AutoE", new CheckBox("Use E on attack"));
            menu.Add("RCombo", new CheckBox("Smart R"));
            menu.Add("RComboFacing", new CheckBox("Only R when allies face enemy", false));


            menu.AddSeparator();
            menu.AddLabel("Misc");
            menu.Add("InterruptQ", new CheckBox("Interrupt Q"));
            menu.Add("AntiGapQ", new CheckBox("Anti Gap Q"));
            menu.Add("HealW", new CheckBox("Use W on low allies"));

            menu.AddSeparator();
            menu.AddLabel("Drawings");
            menu.Add("DrawQ", new CheckBox("Draw Q range"));
            menu.Add("DrawW", new CheckBox("Draw W range", false));
            menu.Add("DrawWBounce", new CheckBox("Draw estimated W-Bounce"));
            menu.AddLabel("For Super Smart W only");

            Game.OnUpdate += GameOnOnUpdate;
            Interrupter.OnInterruptableSpell += InterrupterOnOnInterruptableSpell;
            Gapcloser.OnGapcloser += GapcloserOnOnGapcloser;
            AIHeroClient.OnBasicAttack += AiHeroClientOnOnBasicAttack;
            Drawing.OnDraw += DrawingOnOnDraw;
        }

        private void DrawingOnOnDraw(EventArgs args)
        {
            if (menu.Get<CheckBox>("DrawQ").CurrentValue)
                new Circle { Color = System.Drawing.Color.DodgerBlue, Radius = Q.Range }.Draw(me.Position);

            if (menu.Get<CheckBox>("DrawW").CurrentValue)
                new Circle { Color = System.Drawing.Color.DodgerBlue, Radius = W.Range }.Draw(me.Position);
        }

        private void AiHeroClientOnOnBasicAttack(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.Target is AIHeroClient)
            {
                Core.RepeatAction(() => E.Cast(me), 0, 1000);
            }
            else
            if (sender.IsAlly && sender.Distance(me) <= E.Range && menu.Get<CheckBox>("AutoE").CurrentValue && Q.IsReady() 
                && args.Target is AIHeroClient)
                E.Cast(sender);
        }

        private void GapcloserOnOnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs gapcloserEventArgs)
        {
            if (sender.IsAlly)
                return;

            if (menu.Get<CheckBox>("AntiGapQ").CurrentValue && Q.IsReady())
            {
                if (gapcloserEventArgs.End.Distance(me) <= Q.Range)
                    Q.Cast(gapcloserEventArgs.End);
            }
        }

        private void InterrupterOnOnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs interruptableSpellEventArgs)
        {
            if (sender.IsAlly)
                return;

            if (sender.Distance(me) <= Q.Range && menu.Get<CheckBox>("InterruptQ").CurrentValue && Q.IsReady())
                Q.Cast(interruptableSpellEventArgs.Sender.Position);
        }

        bool EnemyInRange(AIHeroClient ally)
        {
            return EntityManager.Heroes.Enemies.Any(x => x.Distance(ally) <= ally.AttackRange && x.IsValid);
        }

        private void GameOnOnUpdate(EventArgs args)
        {
            var target = TargetSelector.SelectedTarget ?? TargetSelector.GetTarget(1500, DamageType.Physical);
            target = target ?? TargetSelector.GetTarget(1500, DamageType.Magical);

            if (menu.Get<CheckBox>("HealW").CurrentValue && W.IsReady())
            {
                foreach (var ally in EntityManager.Heroes.Allies.Where(x => x.HealthPercent < 20 && 
                    EnemyInRange(x)).OrderBy(x => !x.IsMe).ThenBy(x => x.MaxHealth))
                {
                    if (!ally.IsInShopRange() && !ally.IsRecalling())
                        W.Cast(ally);
                }
            }

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Combo)
            {
                //CheckE();

                if (W.IsReady())
                {
                    if (menu.Get<CheckBox>("WCombo2").CurrentValue)
                        new SuperBrain(menu.Get<CheckBox>("DrawWBounce").CurrentValue).CheckComboW();
                    else if (menu.Get<CheckBox>("WCombo").CurrentValue)
                        WCombo(target);
                }

                if (!E.IsReady())
                    CheckQ(target);

                CheckR(target);
            }
        }

        private void CheckR(AIHeroClient target)
        {
            if (menu.Get<CheckBox>("RCombo").CurrentValue && R.IsReady())
            {
                foreach (var enemy in EntityManager.Heroes.Enemies.Where(x => x.IsValid &&
                    target.Health - me.GetSpellDamage(x, SpellSlot.R) <= 500))
                {
                    var alliesNearby = EntityManager.Heroes.Allies.Count(ally => !ally.IsMe &&
                        ally.Distance(enemy) <= 550);

                    if (alliesNearby < 3)
                        continue;

                    if (!IsAllyFacing(enemy) && menu.Get<CheckBox>("RComboFacing").CurrentValue)
                    {
                        //Chat.Print("no facing ally");
                        continue;
                    }


                    if (cannotMove(enemy))
                    {
                        var ccBuff = GetCCBuff(enemy);
                        var timeLeft = (ccBuff.EndTime - Game.Time) * 1000;
                        var arriveTime = R.CastDelay + (me.Distance(enemy) / R.Speed) * 1000;

                        if (arriveTime > timeLeft && arriveTime < timeLeft + 750)
                            R.Cast(enemy.Position);
                    }
                }
            }
        }

        bool IsAllyFacing(AIHeroClient enemy)
        {
            foreach (var ally in EntityManager.Heroes.Allies.Where(x => !x.IsMe))
            {
                var allyFacingVec = 500 * ally.Direction.To2D().Perpendicular();
                var allyToEnemyVec = enemy.Position - ally.Position;

                if (allyFacingVec.AngleBetween(allyToEnemyVec.To2D()) < 80)
                    return true;
            }

            return false;
        }

        private void CheckQ(AIHeroClient target)
        {
            var qPred = Q.GetPrediction(target);
            if (qPred.HitChance >= HitChance.High && menu.Get<CheckBox>("QCombo").CurrentValue &&
                target.IsValid)
            {
                var targetFacingVec = 500 * target.Direction.To2D().Perpendicular();
                var myFacingVec = 500 * me.Direction.To2D().Perpendicular();

                bool notFacing = targetFacingVec.AngleBetween(myFacingVec) < 90;


                if (notFacing && qPred.CastPosition.Distance(me) < target.Distance(me))
                {
                    //fail predicion
                }
                else
                    Q.Cast(qPred.CastPosition);
            }
        }

        private float Wspeed = 2000;

        bool isEnemyHit(AIHeroClient source, float delay)
        {
            return 
                EntityManager.Heroes.Enemies.Where(x => x.IsValid && x.Distance(source) <= 1500).
                    Select(enemy => Prediction.Position.PredictUnitPosition(enemy, (int) delay)).
                        Any(movePrd => movePrd.Distance(source) <= W.Range - 100);
        }

        bool isAllyHit(AIHeroClient enemy, float delay)
        {
            return
                EntityManager.Heroes.Allies.Where(x => x.IsValid && x.Distance(enemy) <= 1500).
                    Select(ally => Prediction.Position.PredictUnitPosition(ally, (int)delay)).
                        Any(movePrd => movePrd.Distance(enemy) <= W.Range - 100);
        }

        bool cannotMove(AIHeroClient enemy)
        {
            return enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Suppression) ||
                 enemy.HasBuffOfType(BuffType.Snare) || enemy.HasBuffOfType(BuffType.Knockup);
        }

        BuffInstance GetCCBuff(AIHeroClient enemy)
        {
            return enemy.Buffs.OrderByDescending(x => x.EndTime).First(x => x.Type == BuffType.Stun ||
                                                                                x.Type == BuffType.Suppression ||
                                                                                x.Type == BuffType.Knockup);
        }

        private void WCombo(AIHeroClient target)
        {
            int enemyHitVal = 5, targetHitVal = 2
                , allyHealBounceVal = 4, lowAllyVal = 10, allyFullHp = -3, enemyKill = int.MaxValue;

            float bestValue = -1;
            AIHeroClient bestTarget = null;

            foreach (var hero in EntityManager.Heroes.AllHeroes.Where(x => x.IsValid && x.Distance(me) <= W.Range))
            {
                float currentValue = 0;
                float flyTime = W.CastDelay + (me.Distance(hero) / Wspeed) * 1000;

                if (hero.IsAlly)
                {
                    if (hero.IsRecalling() || hero.IsInShopRange())
                        continue;

                    if (isEnemyHit(hero, flyTime))
                        currentValue += enemyHitVal;

                    if (hero.HealthPercent <= 30)
                        currentValue += lowAllyVal;

                    if (hero.HealthPercent >= 75)
                        currentValue += allyFullHp;
                }
                else
                {
                    if (isAllyHit(hero, flyTime))
                        currentValue += allyHealBounceVal;

                    if (hero == target)
                        currentValue += targetHitVal;

                    if (me.GetSpellDamage(hero, SpellSlot.W) > hero.Health)
                        currentValue += enemyKill;
                    else if (me.GetSpellDamage(hero, SpellSlot.W) > hero.Health + me.GetAutoAttackDamage(hero))
                        currentValue += enemyKill*0.75f;

                    if (EntityManager.Heroes.Allies.All(x => x.Distance(hero) > x.AttackRange) &&
                        me.Distance(hero) <= W.Range)
                        currentValue += 100;
                }

                if (currentValue > bestValue)
                {
                    bestValue = currentValue;
                    bestTarget = hero;
                }
                else if (Math.Abs(currentValue - bestValue) <= 0.1f)
                {
                    if (hero.IsAlly)
                    {
                        bestValue = currentValue;
                        bestTarget = hero;
                    }
                }
            }

            bool canKill = bestTarget.IsAlly ? true : me.GetSpellDamage(bestTarget, SpellSlot.W) > bestTarget.Health;

            if (bestTarget.IsValid)
                W.Cast(bestTarget);
            //else Chat.Print("no w target");
        }
    }
}
