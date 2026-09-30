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
Squishiotchi does not collect personal information. Your game is saved on your device. The name you type
stays on your device. For friend visits the game signs in anonymously (a random ID, no email or password)
and shares only your friend code and a snapshot of your squishy and room with players who enter your code.
Reminders are local notifications scheduled on your device. If you buy the full game, the purchase is
handled by Google Play or the App Store; we receive no payment or personal details. There is no chat.
Contact: <your email>.

## Store listing starter text
"Look after your own squishy dumpling in a cosy bamboo-steamer home. Feed it, play, bathe it and tuck it in;
cook recipes, collect 24 room styles and open steamers for surprises. Odds are always shown. Free to try:
one squishy's life or four weeks, then unlock the full game once. No ads in the full game."

## Friends online: one-time setup (you)
1. Sign in at https://cloud.unity.com with your Unity account and create a project called Squishiotchi.
2. In the Unity editor: Edit > Project Settings > Services, pick your organisation and link this project to it
   (this writes the project ID into ProjectSettings; commit that change).
3. In the dashboard for that project, turn on **Authentication** (anonymous sign-in) and **Cloud Save**.
4. Cloud Save > Player Data > Indexes: add two **Public** indexes, key `code` (string) and key `visitTo` (string).
   These let friend codes be looked up and visits be found.
5. The next build then shows Friends as online. Until then the Friends panel says friends aren't switched on,
   and everything else plays normally.

## Still open
- Cloud save of your own game and server time: can use the same Unity project (Cloud Save + Authentication).
  Until then saves are local and the clock guard stops winding the phone clock back.

## Release checklist (owner)

Business and accounts
- [ ] Decide: publish as an individual or as a company (a company keeps your home address off the store page and limits personal liability; see notes below).
- [ ] If a company: register it, get a business bank account, and a free D-U-N-S number (Apple requires one for organisation accounts; Google uses it to verify organisations).
- [ ] Google Play Console account (one-off fee). New *personal* accounts must run a closed test with at least 12 testers for 14 days before they can publish; organisation accounts skip this.
- [ ] Apple Developer Program (yearly fee), plus a Mac or Unity Build Automation for iOS builds.
- [ ] Payout and tax details in both stores (needed for the full-game unlock purchase and any ad revenue).

Legal and compliance
- [ ] Privacy policy hosted at a public web address (text drafted above), and a support email.
- [ ] Age rating questionnaires (IARC on Google Play; Apple's rating form).
- [ ] Target audience: if under-13s are included, Google Play Families rules apply: only Families-certified ad networks, no personalised ads, careful data handling (COPPA in the US, GDPR-K in the EU/UK).
- [ ] Google Play Data safety form and Apple privacy labels (anonymous sign-in for Friends, friend codes, no personal data).
- [ ] Trademark check (and optionally registration) for "Squishiotchi" in your country and main markets.
- [ ] Keep asset credits: Redlight_Chill music (if kept) credited in Settings; Pixabay sounds and music need no credit.
- [ ] Odds shown in game (done); steamers are never sold for real money (keeps clear of paid loot-box rules).

Technical
- [ ] Final app ID (package name): cannot be changed after the first upload.
- [ ] Release signing key (Android keystore) created and backed up in two safe places.
- [ ] Set adminTools false in game_content.json; remove test-only content.
- [x] Link the Unity Cloud project (Friends) and set Cloud Save indexes (done 1 Oct 2026: project Squishiotchi, anonymous sign-in, indexes `code` and `visitTo`).
- [ ] Set up the full-game unlock product in both stores and test a purchase.
- [ ] Ads: pick a Families-certified network or ship without ads.
- [ ] Store listing: icon (done), phone screenshots, feature graphic, short and long description.
- [ ] Final sound and music picks baked in; unused audio removed to shrink the download.


## Setting up as a company in New Zealand (owner; check fees and rules with an accountant)
1. **Decide sole trader or company.** A company (Ltd) keeps your home address off the store page (Google Play shows a physical address for apps that take payments) and limits personal liability. A sole trader is simpler but uses your own name and address.
2. **Register the company** at companies.govt.nz (NZ Companies Office): reserve a name, then incorporate online (small one-off fees). You get an **NZBN** automatically. Check whether a director ID is required when you register.
3. **IRD number for the company**: you can ask for it (and GST) on the incorporation form. **GST** only has to be registered once turnover passes NZ$60,000 in 12 months.
4. **Business bank account** in the company name.
5. **D-U-N-S number** (free, from Dun & Bradstreet; Apple has a lookup and request page). It can take a week or two. Apple needs it for an organisation account; Google uses it to verify organisations.
6. **Store accounts as the organisation**: Google Play Console (organisation accounts skip the 12-tester, 14-day closed test that new personal accounts need) and the Apple Developer Program.
7. **Tax forms in both stores**: US tax forms for a foreign company (the NZ-US tax treaty lowers the US withholding on store payouts). Keep records for the company's NZ tax return, and file the Companies Office annual return each year.

## Friends online: what it needs
No server of your own: friend codes and visits use Unity Gaming Services (Authentication + Cloud Save), which Unity hosts, with a free tier that covers a small game. The one-time setup is the "Friends online" steps above (create the Unity Cloud project, link it in the editor, switch on Authentication and Cloud Save, add the two Public indexes).
