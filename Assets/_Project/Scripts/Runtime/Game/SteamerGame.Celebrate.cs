using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// The big moments get a proper celebration (user request, 29 Sep 2026): the squishy growing a size and a recipe
    /// levelling up. A card with confetti, a rising little fanfare, a happy bounce and a burst of gold glints.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private void CelebrateGrowth(int sizeIdx)
        {
            var size = C.sizes[sizeIdx];
            ui.Celebrate("sq:" + S.favIdx, Rules.Fav.name + " grew!", 0, size.name, "Bigger means squishier: more to press and pinch.");
            Fanfare();
        }

        private void CelebrateRecipe(int recipe, int level)
        {
            var rc = C.recipes[recipe];
            ui.Celebrate("dish:" + recipe, rc.name + " levelled up!", level, null,
                level >= 5 ? "Mastered! Your best cooking yet." : "Cooked better than ever: " + level + " of 5 stars.");
            Fanfare();
        }

        /// <summary>A rising little tune, a happy bounce and gold glints round the squishy.</summary>
        private void Fanfare()
        {
            int[] run = { 0, 2, 4, 5 };
            for (int k = 0; k < run.Length; k++) { int b = run[k]; Later(.1f + k * .12f, () => sfx.Note(b)); }
            Later(.62f, () => sfx.Chime());
            Buzz(20, 40, 20, 40, 60);
            if (mode != "home" || pet == null) return;
            pet.V += 5;
            pet.Express(Models.SquishyModel.Mouth.Grin, 2.5f);
            if (fxPool == null) return;
            var p = PetWorld() + Vector3.up * pet.Scale * .6f;
            for (int i = 0; i < 36; i++)
            {
                float a = Random.value * Mathf.PI * 2;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                fxPool.Spawn(p + dir * pet.Scale * .5f, dir * Rnd(.2f, .5f) + Vector3.up * Rnd(.3f, .8f), Rnd(.06f, .11f), Rnd(1.2f, 2f), 1.2f, -.05f,
                    w: new Vector3(Rnd(0, 6), 0, Rnd(-2, 2)), col: ThreeMat.Hex(i % 3 == 0 ? "#FFFFFF" : "#FFD66B") * 1.3f);
            }
        }
    }
}
