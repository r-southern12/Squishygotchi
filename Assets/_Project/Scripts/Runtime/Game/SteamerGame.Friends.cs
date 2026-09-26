using Squishy.Runtime.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Friends (docs/spec.md, "Friends" proposal): your friend code and where visiting will live. Visiting needs an
    /// online service that isn't switched on yet, so the panel says so plainly instead of pretending.
    /// </summary>
    public sealed partial class SteamerGame
    {
        public void OnFriends()
        {
            ui.SetPanelTitle("info", "Friends");
            var body = ui.PanelBody("info");

            Hud.Sec(body, "Your friend code");
            var chip = new Frame().Set(Css.C("#EADCC6"), 14).Row(Align.Center, Justify.Center).Pad(12, 16, 12, 16).In(body);
            var code = Css.Label(chip, Rules.EnsureFriendCode(), "Gluten", 800, 26, Hud.Ink);
            code.style.letterSpacing = 3;
            Hud.Para(body, "Share it with a friend so they can find your steamer.", 12, "#6F5F52").Margin(6, 0, 12, 0);

            Hud.Sec(body, "Visit a friend");
            var field = new TextField { maxLength = 9, value = "" };
            field.Text("Figtree", 700, 18, Hud.Ink);
            field.SetEnabled(false);
            body.Add(field);
            Hud.Button(body, "Visit", "#EADCC6", "#CDB999", Hud.Ink, 14, 44, 16, null, true, 3).Margin(8, 0, 0, 0);
            Hud.Para(body, "Coming soon: visiting needs our online service, which isn't switched on yet. Then you'll enter a " +
                "friend's code to see how they've decorated, pet and feed their squishy and water their plants. You both " +
                "earn a few coins, and they can do the same for you. No chat, ever.", 12, "#6F5F52").Margin(10, 0, 0, 0);
            ui.OpenPanel("info");
            sfx.Tap();
        }
    }
}
