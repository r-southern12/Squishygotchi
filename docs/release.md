# Release guide

Everything in the game is built; these steps need your accounts. Numbers and ids live in
`Assets/_Project/Resources/Content/game_content.json` (`rules`).

## Before any public build
- Set `"adminTools": false` (hides the hold-the-coins test panel).
- Change the placeholder bundle id `com.squishydumpling.game` (Squishy > Setup > Apply Player Settings) to your own.
- Build: `Squishy > Build > Android APK` (test) or a signed App Bundle for Play (Build Settings, tick "Build App Bundle", set a keystore).

## Google Play (Android)
1. Create a Google Play developer account (one-off $25).
2. Create the app; upload the signed .aab to **Internal testing** (updates then reach testers automatically).
3. Monetisation > Products > In-app products: create **`full_unlock`** (non-consumable, "Full game"). The price you set shows in-game.
4. App content: target audience includes under-13s, so join the **Families** programme: no personalised ads, certified ad SDKs only.
5. Content rating questionnaire (expected: PEGI 3 / Everyone; "simulated gambling": no, odds are disclosed and no real-money loot boxes).

## Apple (iPhone) without a Mac
1. Apple Developer Program ($99/year).
2. Build in the cloud: **Unity Build Automation** (Unity dashboard) or **Codemagic** pointed at the GitHub repo; they run Xcode on a Mac for you.
3. App Store Connect: create the app, an in-app purchase **`full_unlock`** (non-consumable), TestFlight for testing.
4. Kids Category rules: no third-party ads or analytics that track; parental gate before purchases.
5. iOS widget: needs a WidgetKit extension written in Swift, added in the cloud build (the Android widget is done).

## Ads (optional rewarded videos only, no pop-ups)
- The gift steamer already offers an optional video to free players. Until an ad account exists, the reward is granted straight away (test mode).
- To go live: create a Unity LevelPlay (or AdMob) account set to **child-directed / non-personalised**, put the app ids in `adsGameIdAndroid` / `adsGameIdIos`, and ask for the SDK to be wired into `Ads.ShowRewarded` in `Store.cs`.

## Privacy policy (draft to host on a web page and link in both stores)
Squishy Dumpling does not collect personal information. Your game is saved on your device only.
Reminders are local notifications scheduled on your device. If you buy the full game, the purchase is
handled by Google Play or the App Store; we receive no payment or personal details. There is no chat and
no account. Contact: <your email>.

## Store listing starter text
"Look after your own squishy dumpling in a cosy bamboo-steamer home. Feed it, play, bathe it and tuck it in;
cook recipes, collect 24 room styles and open steamers for surprises. Odds are always shown. Free to try:
one squishy's life or four weeks, then unlock the full game once. No ads in the full game."

## Still open (need cloud services)
- Cloud save and server time: needs a backend (e.g. Unity Cloud Save + Authentication, linked to your Unity
  dashboard project). Until then saves are local and the clock guard stops winding the phone clock back.
