using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Rendering;
using SharpDX;

namespace MoonyNami_EB
{
    static class SuperBrain
    {
        private static float lastDelayTime = 0;
        private static int lastDrawEndTick = 0;

        public static void IllustrateValues()
        {
            if (Environment.TickCount - lastDrawEndTick < lastDelayTime)
                return;

            int i = 0;
            foreach (KeyValuePair<AIHeroClient, float> val in wValuesToDraw)
            {
                var hero = val.Key;
                var value = val.Value;

                Random rand = new Random();
                ColorBGRA drawColor = new ColorBGRA(new Vector3(rand.Next(100) / 100, rand.Next(100) / 100, 
                    rand.Next(100) / 100), 1F);

                if (wValuesToDraw.Values.Last().Equals(value))
                {
                    //last entry reached
                }
                else
                {
                    var nextHero = wValuesToDraw.Keys.ToList()[i + 1];
                    //doesnt implement movement predicted position
                    float flyTime = (hero.Distance(nextHero) / (Wspeed + 500)) * 1000;

                    Core.DelayAction(() =>
                        new Circle(drawColor, 100, i*3).Draw(nextHero.Position), (int)flyTime
                    );

                    lastDelayTime += (int)flyTime;
                }

                if (i == 0)
                    new Circle(drawColor, 100).Draw(hero.Position);

                
                i++;
            }

            Core.DelayAction(() => wValuesToDraw.Clear(), (int)lastDelayTime);
            lastDrawEndTick = Environment.TickCount;
        }

        readonly static Spell.Targeted W = new Spell.Targeted(SpellSlot.W, 725);
        private static float Wspeed = 1600;
        /// <summary>
        /// restHp Value (from 0 - 10)
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        static float GetMissHpValue(float hpMissingPercent)
        {
            return (float)Math.Pow(1.02329299f, hpMissingPercent);
        }

        //1 per 3000 hp
        static float GetMaxHealthDecreaseValue(float maxhealth)
        {
            return maxhealth / 3000;
        }

        static List<AIHeroClient> GetWHeroes(AIHeroClient source, bool wStartDelay = false)
        {
            List<AIHeroClient> wHeroes = new List<AIHeroClient>();
            foreach (var hero in EntityManager.Heroes.AllHeroes.Where(x => x.IsValid))
            {
                float delay = (source.Distance(hero) / Wspeed) * 1000;
                delay += wStartDelay ? W.CastDelay : 0;
                var pred = Prediction.Position.PredictUnitPosition(hero, (int)delay);

                if (source.Distance(pred) <= W.Range)
                    wHeroes.Add(hero);
            }

            return wHeroes;
        }

        /// <summary>
        /// Returns initHero + total value
        /// </summary>
        /// <param name="target"></param>
        /// <param name="involvedHeroes"></param>
        /// <param name="currentValue"></param>
        /// <param name="initHero"></param>
        /// <returns></returns>
        static Tuple<AIHeroClient, float> AddWTarget(AIHeroClient target, List<AIHeroClient> involvedHeroes, float currentValue,
             AIHeroClient initHero)
        {
            involvedHeroes.Add(target);

            currentValue += GetMissHpValue(100 - target.HealthPercent);
            currentValue -= GetMaxHealthDecreaseValue(target.MaxHealth);

            var nextHeroes = GetWHeroes(target);

            if (nextHeroes.Any(x => !involvedHeroes.Contains(x) && x.Team != target.Team))
            {
                var nextHero = nextHeroes.OrderBy(x => x.Distance(target)).First(x =>
                    !involvedHeroes.Contains(x) && x.Team != target.Team);
                return AddWTarget(nextHero, involvedHeroes, currentValue, initHero);
            }

            return new Tuple<AIHeroClient, float>(initHero, currentValue);
        }

        static void Debug(float val, AIHeroClient initHero)
        {
            Chat.Print(initHero.ChampionName + " value: " + val);
        }

        static Dictionary<AIHeroClient, float> wValuesToDraw = new Dictionary<AIHeroClient, float>();
        public static void CheckComboW()
        {
            Dictionary<AIHeroClient, float> wValues = new Dictionary<AIHeroClient, float>();

            foreach (var hero in EntityManager.Heroes.AllHeroes.Where(x => x.IsValid && x.Distance(ObjectManager.Player) 
                <= W.Range))
            {
                List<AIHeroClient> involvedHeroes = new List<AIHeroClient>();
                float startVal = 0f;

                var endValue = AddWTarget(hero, involvedHeroes, startVal, hero);
                wValues.Add(endValue.Item1, endValue.Item2);
            }

            wValuesToDraw = wValues;

            if (wValues.Any())
            {
                var bestInitHero = wValues.OrderByDescending(x => x.Value).First().Key;
                //Debug(wValues.OrderByDescending(x => x.Value).First().Value, bestInitHero);
                if (!(bestInitHero.IsMe && ObjectManager.Player.HealthPercent >= 85))
                    W.Cast(bestInitHero);
                else
                    wValuesToDraw.Clear();
            }
        }
    }
}
