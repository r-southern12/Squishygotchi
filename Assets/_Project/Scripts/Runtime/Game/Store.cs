using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// The one-off "full game" unlock (Unity IAP, non-consumable). Uses the fake store in the editor; on devices it
    /// needs the product set up in Google Play / App Store Connect with the id from game_content.json.
    /// </summary>
    public sealed class Store : IDetailedStoreListener
    {
        private readonly string _id;
        private readonly Action _onOwned;
        private IStoreController _c;
        private IExtensionProvider _e;

        public string Price { get; private set; }

        public Store(string productId, Action onOwned)
        {
            _id = productId;
            _onOwned = onOwned;
            try
            {
                var b = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                b.AddProduct(_id, ProductType.NonConsumable);
                UnityPurchasing.Initialize(this, b);
            }
            catch (Exception ex) { Debug.LogWarning("Store unavailable: " + ex.Message); }
        }

        public void Buy()
        {
            if (_c == null) { Debug.LogWarning("Store not ready yet."); return; }
            _c.InitiatePurchase(_id);
        }

        public void Restore()
        {
            if (_e == null) return;
#if UNITY_IOS
            _e.GetExtension<IAppleExtensions>().RestoreTransactions((ok, msg) => { });
#else
            // Google Play restores automatically on start: owned purchases come back through OnInitialized.
            if (_c != null) Check();
#endif
        }

        private void Check()
        {
            var p = _c.products.WithID(_id);
            if (p != null && p.hasReceipt) _onOwned();
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _c = controller;
            _e = extensions;
            var p = _c.products.WithID(_id);
            if (p != null) Price = p.metadata.localizedPriceString;
            Check();
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == _id) _onOwned();
            return PurchaseProcessingResult.Complete;
        }

        public void OnInitializeFailed(InitializationFailureReason error) { Debug.LogWarning("Store init failed: " + error); }
        public void OnInitializeFailed(InitializationFailureReason error, string message) { Debug.LogWarning("Store init failed: " + error + " " + message); }
        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) { Debug.LogWarning("Purchase failed: " + reason); }
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription d) { Debug.LogWarning("Purchase failed: " + d.reason + " " + d.message); }
    }

    /// <summary>
    /// Optional rewarded videos (no pop-ups, no forced ads). Until an ad network account is set up and its ids are in
    /// game_content.json, rewards are granted straight away so the flow can be tested.
    /// </summary>
    public static class Ads
    {
        private static bool _configured;

        public static void Init(string androidId, string iosId)
        {
#if UNITY_IOS
            _configured = !string.IsNullOrEmpty(iosId);
#else
            _configured = !string.IsNullOrEmpty(androidId);
#endif
        }

        public static void ShowRewarded(Action<bool> done)
        {
            if (!_configured) { done(true); return; } // no ad network yet: test mode grants the reward
            // Network SDK call goes here once an account exists (child-directed, non-personalised ads only).
            done(true);
        }
    }
}
