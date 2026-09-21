using UnityEngine;

namespace WarcraftTD
{
    public sealed class FirstMissionFlow : MonoBehaviour
    {
        private PrototypeGame game;
        private bool damageTipShown;
        private bool routeTipShown;

        public void Initialize(PrototypeGame owner)
        {
            game = owner;
        }

        public void Tick()
        {
            if (game == null || game.IsFinished) return;

            if (!game.IsWaveStarted)
            {
                if (!damageTipShown && game.PreparationRemaining <= 45f)
                {
                    damageTipShown = true;
                    game.SetStatus("Лучники наносят обычный урон. Баллисты — пронзающий, особенно сильный против лёгкой брони.");
                }
                else if (!routeTipShown && game.PreparationRemaining <= 25f)
                {
                    routeTipShown = true;
                    game.SetStatus("Красная линия показывает путь врагов. После начала волны она станет зелёной.");
                }
                return;
            }

            if (game.ShouldPaladinIntervene())
                game.StartPaladinRescue();
        }
    }
}
