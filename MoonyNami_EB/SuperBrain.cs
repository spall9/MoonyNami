using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Rendering;
using SharpDX;

namespace MoonyNami_EB
{
    class SuperBrain
    {
        public SuperBrain(bool drawBounces)
        {
            if (drawBounces)
                Drawing.OnDraw += IllustrateValues;
        }

        ~SuperBrain()
        {
            Drawing.OnDraw -= IllustrateValues;
        }

        private float lastDelayTime = 0;
        private int lastDrawEndTick = 0;

        private void IllustrateValues(EventArgs args)
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

        readonly Spell.Targeted W = new Spell.Targeted(SpellSlot.W, 725);
        private float Wspeed = 1600;
        /// <summary>
        /// restHp Value (from 0 - 10)
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        float GetMissHpValue(float hpMissingPercent)
        {
            return (float)Math.Pow(1.02329299f, hpMissingPercent);
        }

        //1 per 3000 hp
        float GetMaxHealthDecreaseValue(float maxhealth)
        {
            return maxhealth / 3000;
        }

        List<AIHeroClient> GetWHeroes(AIHeroClient source, bool wStartDelay = false)
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
        Tuple<AIHeroClient, float> AddWTarget(AIHeroClient target, List<AIHeroClient> involvedHeroes, float currentValue,
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

        void Debug(float val, AIHeroClient initHero)
        {
            Chat.Print(initHero.ChampionName + " value: " + val);
        }

        Dictionary<AIHeroClient, float> wValuesToDraw = new Dictionary<AIHeroClient, float>();
        public void CheckComboW()
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
                W.Cast(bestInitHero);
            }
        }
    }
}
