using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    public sealed partial class SteamerGame
    {
        private sealed class UCam
        {
            public Vector3 ap, at, bp, bt, t = new Vector3(0, .7f, 0);
            public float k = 1, dur = 1, half = .7f;
            public string kind = "closed";
        }

        private readonly UCam ucam = new UCam();
        private Transform box, lid, stageG, prize, plate, rays, rimGlow;
        private Material rimMat, raysMat;
        private SquishyModel newbie;
        private string ustate = "idle";
        private float ust, charge, raysOn, _warm;
        private bool holding;
        private int lastTick;
        private Reward reward;
        private Vector3 lidP, lidV, lidR, lidW;
        private bool lidOn;
        private float nbY, nbVy, nbSpin, prizeRotY, newbieYaw;
        private bool nbAir;
        private static readonly Vector3 LidRest = new Vector3(0, H + .02f, 0);
        private static readonly string[] Conf = { "#C8674E", "#D9A64A", "#8FAE7E", "#6E9C9A", "#E8A796", "#EFE2C9", "#FFFFFF" };

        private void BuildUnbox()
        {
            for (int i = -9; i <= 9; i++)
            {
                var col = ThreeMat.OffsetHsl("#A87A4F", 0, Rnd(-.03f, .03f), Rnd(-.04f, .04f));
                Node.Mesh(unbox, ThreeGeo.RBox(.7f, .3f, 10, .05f), ThreeMat.Lambert(col), i * .72f, -.15f, -1.5f, shadow: false, receive: true);
            }
            var wallMat = ThreeMat.Lambert(Color.white, null, Textures.Tiles());
            wallMat.SetTextureScale("_BaseMap", new Vector2(8, 3));
            wallMat.SetFloat("_ReceiveShadows", 0);
            var back = Node.Mesh(unbox, ThreeGeo.Plane(26, 10), wallMat, 0, 2.3f, -4.2f, shadow: false);
            back.localScale = Vector3.one * .55f;
            Jar(-2, -2.7f, .7f, .27f, "#6E9C9A", "#8A5D3B");
            Jar(-1.4f, -3.1f, .5f, .22f, "#D9A64A", "#8A5D3B");
            Jar(1.8f, -2.9f, .85f, .3f, "#C8674E", "#EFE2C9");
            Jar(2.4f, -2.2f, .45f, .2f, "#8FAE7E", "#8A5D3B");
            var stack = Node.Group(unbox, "stack", -2.3f, 0, -1.3f);
            new SteamerModel(stack, 36);
            SteamerModel.Lid(stack).localPosition = new Vector3(0, H + .02f, 0);
            stack.localScale = Vector3.one * .34f;
            var ub = Node.Group(unbox, "ub");
            ub.localScale = Vector3.one * US;
            box = Node.Group(ub, "box");
            new SteamerModel(box, 60);
            rimMat = ThreeMat.Basic(ThreeMat.Lin("#FFB14D"), 0, ThreeMat.Blend.Additive, depthWrite: false);
            rimGlow = Node.Mesh(box, ThreeGeo.Torus(R + .03f, .1f, 6, 56), rimMat, 0, H + .05f, 0, shadow: false);
            rimGlow.RotX(Mathf.PI / 2);
            lid = SteamerModel.Lid(ub);
            lid.gameObject.SetActive(false);
            stageG = Node.Group(unbox, "stage");
            newbie = new SquishyModel(stageG, .2f);
            newbie.Pivot.gameObject.SetActive(false);
            prize = Node.Group(stageG, "prize");
            plate = Node.Group(stageG, "plate");
            Node.Mesh(plate, ThreeGeo.Cyl(.36f, .33f, .04f, 32), ThreeMat.M("#F4EBDD"), 0, .02f, 0);
            Node.Mesh(plate, ThreeGeo.Torus(.35f, .018f, 6, 40), ThreeMat.M("#D9A64A"), 0, .04f, 0, shadow: false).RotX(Mathf.PI / 2);
            plate.gameObject.SetActive(false);
            unSteam = new ParticlePool(110, ThreeGeo.Ico1(), SteamMat(), false, UnboxLayer);
            confetti = new ParticlePool(80, ThreeGeo.PlaneMirrored(.12f, .17f), ThreeMat.Basic(Color.white, 1, ThreeMat.Blend.Opaque, null, true), true, UnboxLayer);
            raysMat = ThreeMat.Basic(ThreeMat.Lin("#FFE3A8"), 0, ThreeMat.Blend.Additive, Textures.Rays(), false, false);
            raysMat.renderQueue = 3100;
            rays = Node.Mesh(unbox, ThreeGeo.Plane(2.4f, 2.4f), raysMat, 0, 0, 0, shadow: false);
            Node.SetLayer(unbox, UnboxLayer);
        }

        private void Jar(float x, float z, float h, float r, string col, string lidCol)
        {
            var g = Node.Group(unbox, "jar", x, 0, z);
            Node.Mesh(g, ThreeGeo.Cyl(r, r, h, 16), ThreeMat.M(col), 0, h / 2, 0, shadow: false);
            Node.Mesh(g, ThreeGeo.Cyl(r * .8f, r * .85f, .16f, 16), ThreeMat.M(lidCol), 0, h + .08f, 0, shadow: false);
        }

        private void UPuff(Vector3 p, Vector3 v, float s, float life, float drag = 1.5f, float g = .8f) { unSteam.Spawn(p, v, s, life, drag, g); }
        private void UPuffL(Vector3 p, Vector3 v, float s, float life, float drag = 1.5f, float g = .8f) { unSteam.Spawn(p * US, v * US, s * US, life, drag, g * US); }

        private void UPose(string kind, out Vector3 p, out Vector3 t)
        {
            float half = kind == "closed" ? R * US * 1.7f : ucam.half, d = FitDist(half), el = (kind == "closed" ? 38 : 50) * Mathf.Deg2Rad;
            t = new Vector3(0, kind == "closed" ? .7f * US : Y0 * US + ucam.half * .5f, 0);
            p = new Vector3(0, Mathf.Sin(el) * d, Mathf.Cos(el) * d) + t;
        }

        private void UMove(string kind, float dur)
        {
            ucam.kind = kind;
            ucam.ap = Space3.U(cam.transform.position);
            ucam.at = ucam.t;
            UPose(kind, out ucam.bp, out ucam.bt);
            ucam.k = 0;
            ucam.dur = dur;
        }

        public void EnterUnbox()
        {
            if (S.dead) return;
            CloseCook();
            if (S.steamers <= 0) { ui.SetHint("No steamers. Earn them from tasks or buy one.", true); OpenShop(); return; }
            WipeTo(() =>
            {
                SetMode("unbox");
                ui.HideCard();
                raysOn = 0;
                ThreeMat.SetOpacity(raysMat, 0);
                newbie.Pivot.gameObject.SetActive(false);
                ClearPrize();
                ucam.kind = "closed";
                UPose("closed", out ucam.bp, out ucam.bt);
                PlaceCamera(ucam.bp, ucam.bt);
                ucam.t = ucam.bt;
                ucam.k = 1;
                SceneLighting.ClearLamps();
                DropLid();
            });
        }

        public void GoHome()
        {
            WipeTo(() =>
            {
                ui.HideCard();
                raysOn = 0;
                ui.ShowHud(true);
                sfx.Hum(0);
                charge = 0;
                holding = false;
                ustate = "idle";
                SetMode("home");
                PlaceLamps();
                if (grewTo.HasValue)
                {
                    growAnim = (pet.Scale, C.sizes[grewTo.Value].s, 0);
                    Floater("Grew to " + C.sizes[grewTo.Value].name + "!");
                    sfx.Chime();
                    grewTo = null;
                    UpdateSub();
                }
                WriteSave();
            });
        }

        private void DropLid()
        {
            ustate = "lidIn";
            ust = 0;
            ui.ShowHud(true);
            ui.SetHint("");
            ui.SetRing(0);
            lid.gameObject.SetActive(true);
            lidP = new Vector3(0, H + 8, 0);
            lidV = Vector3.zero;
            lidR = Vector3.zero;
            lidOn = true;
        }

        private void LidLanded()
        {
            ustate = "closed";
            ust = 0;
            shake = .35f;
            Buzz(25);
            sfx.Thunk();
            newbie.Pivot.gameObject.SetActive(false);
            ClearPrize();
            plate.gameObject.SetActive(false);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2;
                UPuffL(new Vector3(Mathf.Cos(a) * (R + .2f), .25f, Mathf.Sin(a) * (R + .2f)), new Vector3(Mathf.Cos(a) * 2.4f, .6f, Mathf.Sin(a) * 2.4f), Rnd(.18f, .3f), Rnd(.5f, .8f), 3, .5f);
            }
            ui.SetHint("Hold anywhere to build steam", true);
            UpdatePity();
        }

        private void UpdatePity()
        {
            ui.SetPity("Rare or better within " + (C.rules.pityRare - S.sinceRare) + " · Epic or better within " + (C.rules.pityEpic - S.sinceEpic), "Odds · Rare in " + (C.rules.pityRare - S.sinceRare));
        }

        private void ClearPrize() { foreach (Transform c in prize) Node.Destroy(c); }

        public void HoldStart()
        {
            if (mode == "unbox" && (ustate == "closed" || ustate == "charging")) { holding = true; ustate = "charging"; ui.MainDown(true); }
        }

        public void HoldEnd() { holding = false; ui.MainDown(false); }

        private void Pop()
        {
            ustate = "pop";
            ust = 0;
            charge = 0;
            holding = false;
            sfx.Hum(0);
            ui.MainDown(false);
            ui.ShowHud(false);
            shake = 1;
            flash = .9f;
            Buzz(40, 30, 80);
            sfx.Pop();
            lidOn = true;
            lidV = new Vector3(Rnd(-.6f, .6f), 13, Rnd(-1.5f, -.5f));
            lidW = new Vector3(Rnd(-4, -2), Rnd(-3, 3), Rnd(-3, 3));
            for (int i = 0; i < 40; i++)
            {
                float a = Rnd(0, Mathf.PI * 2), sp = Rnd(2, 6);
                UPuffL(new Vector3(Mathf.Cos(a) * Rnd(0, R * .8f), H + .3f, Mathf.Sin(a) * Rnd(0, R * .8f)), new Vector3(Mathf.Cos(a) * sp, Rnd(2, 7), Mathf.Sin(a) * sp), Rnd(.3f, .6f), Rnd(.9f, 1.5f), 1.8f, .6f);
            }
            for (int i = 0; i < 80; i++)
            {
                float a = Rnd(0, Mathf.PI * 2), sp = Rnd(1.5f, 4.5f);
                confetti.Spawn(new Vector3(Rnd(-.2f, .2f), (H + .5f) * US, Rnd(-.2f, .2f)), new Vector3(Mathf.Cos(a) * sp * .55f, Rnd(3.4f, 6), Mathf.Sin(a) * sp * .55f), 1, Rnd(2.2f, 3), 1.1f, -5, .01f,
                    new Vector3(Rnd(0, 6), Rnd(0, 6), Rnd(0, 6)), new Vector3(Rnd(-10, 10), Rnd(-10, 10), Rnd(-10, 10)), ThreeMat.Lin(Conf[i % Conf.Length]));
            }
            Rules.SetSteamers(S.steamers - 1);
            reward = Rules.RollReward();
            ClearPrize();
            newbie.Pivot.gameObject.SetActive(false);
            if (reward.type == "sq") { newbie.SetFinish(C.finishes[reward.i]); ucam.half = .5f; }
            else
            {
                var holder = Node.Group(prize, "holder");
                Transform obj;
                if (reward.type == "item") { var a = reward.key.Split(':'); obj = ItemModels.Build(C, a[0], a[1], holder, new ItemParts()); }
                else if (reward.type == "skin") { obj = Node.Group(holder, "steamer"); new SteamerModel(obj, 28).Skin(C.skins[reward.i]); }
                else if (reward.type == "tskin") obj = KitchenModels.Tool(C, S, reward.i, reward.j, holder);
                else if (reward.type == "tool") obj = KitchenModels.Tool(C, S, reward.i, null, holder);
                else obj = KitchenModels.Food(C, reward.i, holder);
                // Normalise every prize to the same display size and stand it on the plate.
                var bb = Node.LocalBounds(obj, holder);
                float target = reward.type == "item" ? .6f : .42f, k = target / Mathf.Max(bb.size.x, Mathf.Max(bb.size.y * .8f, bb.size.z));
                obj.localScale *= k;
                bb = Node.LocalBounds(obj, holder);
                obj.localPosition += new Vector3(-bb.center.x, .045f - bb.min.y, -bb.center.z);
                Node.SetLayer(prize, UnboxLayer);
                prize.gameObject.SetActive(false);
                ucam.half = .55f;
            }
            UMove("reveal", reduce ? .3f : 1.4f);
            WriteSave();
        }

        private void Launch()
        {
            ustate = "launch";
            ust = 0;
            nbY = H * US * .4f;
            nbVy = 6;
            nbAir = true;
            nbSpin = 0;
            plate.gameObject.SetActive(true);
            if (reward.type == "sq") { newbie.Pivot.gameObject.SetActive(true); newbie.X = -.5f; newbie.V = 0; }
            else prize.gameObject.SetActive(true);
            sfx.Tap();
        }

        private void Landed()
        {
            nbAir = false;
            nbY = 0;
            newbie.X = .95f;
            newbie.V = 0;
            ustate = "landed";
            ust = 0;
            shake = .45f;
            Buzz(30);
            sfx.Land();
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2;
                UPuff(new Vector3(Mathf.Cos(a) * .28f, Y0 * US + .05f, Mathf.Sin(a) * .28f), new Vector3(Mathf.Cos(a) * 1.3f, .25f, Mathf.Sin(a) * 1.3f), Rnd(.05f, .09f), Rnd(.4f, .6f), 3.5f, .3f);
            }
        }

        private void ShowCard()
        {
            ustate = "card";
            raysOn = 1;
            sfx.Chime();
            Buzz(15);
            var card = Rules.Claim(reward);
            if (card.delayedCoins > 0) { int n = card.delayedCoins; Later(.4f, () => AddCoins(n)); }
            if (card.grewTo >= 0) grewTo = card.grewTo;
            if (card.kitchenChanged) DecorateStoves();
            ui.ShowCard(card.isNew, card.name, card.tier, card.dot, card.meta, S.steamers > 0 ? "Unbox again (" + S.steamers + ")" : "Get steamers");
            WriteSave();
        }

        public void CardAgain()
        {
            if (S.steamers <= 0) { GoHome(); Later(.5f, OpenShop); return; }
            // Clear the last prize straight away so it never shows inside the next steamer.
            ClearPrize();
            plate.gameObject.SetActive(false);
            newbie.Pivot.gameObject.SetActive(false);
            prize.gameObject.SetActive(false);
            ui.HideCard();
            raysOn = 0;
            sfx.Tap();
            UMove("closed", reduce ? .3f : .8f);
            DropLid();
        }

        private void StepUnbox(float dt)
        {
            ust += dt;
            if (ucam.k < 1)
            {
                ucam.k = Mathf.Min(1, ucam.k + dt / ucam.dur);
                float k = InOutCubic(ucam.k);
                ucam.t = Vector3.Lerp(ucam.at, ucam.bt, k);
                PlaceCamera(Vector3.Lerp(ucam.ap, ucam.bp, k), ucam.t);
            }
            else PlaceCamera(ucam.bp, ucam.t);
            if (ustate == "lidIn" && lidOn && ust > .3f)
            {
                lidV.y -= 26 * dt;
                lidP += lidV * dt;
                if (lidP.y <= LidRest.y) { lidP = LidRest; lidOn = false; LidLanded(); }
            }
            if (lidOn && ustate != "lidIn")
            {
                lidV.y -= 16 * dt;
                lidP += lidV * dt;
                lidR += lidW * dt;
                // Gone once it tops out and the camera zooms in on the prize (the prototype let it fall back through).
                if (lidP.y > 14 || lidV.y < 0) { lidOn = false; lid.gameObject.SetActive(false); }
            }
            bool charging = ustate == "closed" || ustate == "charging";
            if (charging)
            {
                charge = holding ? Mathf.Min(1, charge + dt * .55f) : Mathf.Max(0, charge - dt * .6f);
                ui.SetRing(charge);
                int tick = Mathf.FloorToInt(charge * 4);
                if (tick > lastTick) Buzz(10 + tick * 8);
                lastTick = tick;
                ui.SetHint(charge < .05f ? "Hold anywhere to build steam" : charge < .45f ? "Heating up…" : charge < .8f ? "Nearly there!" : "Hold it!!", charge < .05f || charge > .8f);
                sfx.Hum(holding ? charge : 0);
                float rate = (1 + charge * charge * 30) * dt;
                int n = Mathf.FloorToInt(rate) + (Random.value < rate % 1 ? 1 : 0);
                for (int i = 0; i < n; i++)
                {
                    float a = Rnd(0, Mathf.PI * 2);
                    UPuffL(new Vector3(Mathf.Cos(a) * (R + .1f), H + .25f + Rnd(0, .2f), Mathf.Sin(a) * (R + .1f)),
                        new Vector3(Mathf.Cos(a) * Rnd(.3f, 1.2f) * (1 + charge * 2), Rnd(1, 2.5f) + charge * 3, Mathf.Sin(a) * Rnd(.3f, 1.2f) * (1 + charge * 2)), Rnd(.16f, .28f) + charge * .25f, Rnd(.8f, 1.2f), 1.2f, .9f);
                }
                if (charge >= 1) Pop();
            }
            float c2 = charging ? charge * charge : 0, amp = reduce ? 0 : c2 * .06f;
            box.localPosition = new Vector3(Rnd(-amp, amp), 0, Rnd(-amp, amp));
            box.RotZ(reduce ? 0 : Rnd(-1f, 1f) * c2 * .015f);
            ThreeMat.SetOpacity(rimMat, c2 * .9f);
            _warm = c2;
            if (charging)
            {
                lid.localPosition = LidRest + box.localPosition + new Vector3(0, Mathf.Abs(Mathf.Sin(time * (14 + charge * 24))) * c2 * .14f, 0);
                Node.Rot(lid, Rnd(-1f, 1f) * c2 * .03f, 0, Rnd(-1f, 1f) * c2 * .03f);
            }
            else if (lid.gameObject.activeSelf) { lid.localPosition = lidP; Node.Rot(lid, lidR.x, lidR.y, lidR.z); }
            if (ustate == "pop" && ust > .35f) Launch();
            if (nbAir) { nbVy -= 24 * dt; nbY += nbVy * dt; nbSpin += dt * 14; if (nbVy < 0 && nbY <= 0) Landed(); }
            float spin = nbAir ? nbSpin : 0;
            if (reward != null && reward.type == "sq")
            {
                if (nbAir) newbieYaw = spin;
                else { float d = -newbieYaw; d = Mathf.Atan2(Mathf.Sin(d), Mathf.Cos(d)); newbieYaw += d * Mathf.Min(1, dt * 6); }
                newbie.Yaw.RotY(newbieYaw);
            }
            else { prizeRotY = nbAir ? spin : prizeRotY + dt * .6f; prize.RotY(prizeRotY); }
            if (ustate == "landed" && ust > .7f) ShowCard();
            newbie.Update(dt, 0, false);
            plate.localPosition = new Vector3(0, Y0 * US + .01f, 0);
            newbie.Pivot.localPosition = new Vector3(0, Y0 * US + .05f + Mathf.Max(0, nbY), 0);
            prize.localPosition = new Vector3(0, Y0 * US + .01f + Mathf.Max(0, nbY), 0);
            float tgt = raysOn > 0 ? .8f : 0;
            float op = raysMat.GetVector("_BaseColor").w;
            op += (tgt - op) * Mathf.Min(1, dt * 4);
            ThreeMat.SetOpacity(raysMat, op);
            rays.gameObject.SetActive(op > .01f);
            if (op > .01f)
            {
                // Face the camera, sit .8 behind the centre, spin, and scale with the reveal.
                var centre = new Vector3(0, Y0 * US + ucam.half * .5f, 0);
                var camP = Space3.U(cam.transform.position);
                var toCam = (camP - centre).normalized;
                rays.localPosition = centre - toCam * .8f;
                rays.localRotation = Quaternion.LookRotation(toCam, Vector3.up) * Quaternion.AngleAxis(time * .4f * Mathf.Rad2Deg, Vector3.forward);
                rays.localScale = Vector3.one * (ucam.half * 1.6f);
            }
        }
    }
}
