using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
namespace Coup.Net
{
    public sealed class Storefront : MonoBehaviour
    {
        private StoreController _store;
        private string _account;
        private TaskCompletionSource<bool> _productsReady;
        private List<ProductDefinition> _definitions;
        private readonly HashSet<string> _verifying = new HashSet<string>();
        public string Status { get; private set; } = "Store not connected.";
        public bool Busy { get; private set; }
        public event Action Changed;
        public IReadOnlyList<Product> Products => _store?.GetProducts()?.ToList() ?? new List<Product>();
        public static Storefront Instance {
            get { var platform = PlatformClient.Instance; var value = platform.GetComponent<Storefront>(); return value != null ? value : platform.gameObject.AddComponent<Storefront>(); }
        }
        private void SetStatus(string message) { Status = message; Changed?.Invoke(); }
        public async Task Initialize(List<JsonValue> catalog) {
#if UNITY_EDITOR
            SetStatus("Purchases are available in App Store and Google Play builds. No charges can be made in the Editor.");
            await Task.CompletedTask; return;
#else
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer) { SetStatus("This platform does not support the mobile store."); return; }
            if (_store != null) {
                if (_account != PlatformClient.Instance.AccountId) throw new InvalidOperationException("Restart the app to switch store accounts safely.");
                if (Products.Count == 0 && _definitions != null) {
                    _productsReady = new TaskCompletionSource<bool>(); _store.FetchProducts(_definitions); await WaitForProducts();
                } else _store.FetchPurchases();
                return;
            }
            if (catalog == null || catalog.Count == 0) { SetStatus("The shop has no offers available."); return; }
            var account = PlatformClient.Instance.AccountId;
            if (string.IsNullOrEmpty(account)) throw new InvalidOperationException("Sign in before opening the store.");
            _productsReady = new TaskCompletionSource<bool>();
            _account = account; Busy = true; SetStatus("Connecting to your store…");
            _store = UnityIAPServices.StoreController();
            _store.OnPurchasePending += OnPending;
            _store.OnPurchaseConfirmed += _ => { Busy = false; SetStatus("Purchase completed. Your balance is up to date."); };
            _store.OnPurchaseFailed += _ => { Busy = false; SetStatus("Purchase cancelled or declined. You have not received any items."); };
            _store.OnPurchaseDeferred += _ => { Busy = false; SetStatus("Purchase is awaiting approval. Items will appear after approval and verification."); };
            _store.OnStoreDisconnected += _ => { Busy = false; SetStatus("Store disconnected. Open the shop again to retry."); };
            _store.OnPurchasesFetched += orders => { foreach (var order in orders.PendingOrders) OnPending(order); };
            _store.OnPurchasesFetchFailed += _ => { Busy = false; SetStatus("Could not check pending purchases. Tap Restore to try again."); };
            _store.OnProductsFetchFailed += _ => { _productsReady?.TrySetException(new InvalidOperationException("Offers could not be loaded.")); Busy = false; SetStatus("Offers could not be loaded. Try again later."); };
            _store.OnProductsFetched += _ => { _productsReady?.TrySetResult(true); Busy = false; SetStatus("Choose an offer. Prices are provided by your store."); _store.FetchPurchases(); };
            try {
                await _store.Connect();
                _store.AppleStoreExtendedService?.SetAppAccountToken(Guid.Parse(account));
                using (var sha = SHA256.Create()) _store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant());
                var provider = Application.platform == RuntimePlatform.IPhonePlayer ? "apple" : "google";
                var definitions = catalog.Where(p => !string.IsNullOrEmpty(p.Str(provider))).Select(p => new ProductDefinition(p.Str("id"),p.Str(provider),ProductType.Consumable)).ToList();
                if (definitions.Count == 0) { Busy = false; SetStatus("No offers are available for this store."); return; }
                _definitions = definitions; _store.FetchProducts(definitions); await WaitForProducts();
            } catch { Busy = false; SetStatus("The store could not connect. Please try again."); throw; }
#endif
        }
        private async Task WaitForProducts() {
            if (await Task.WhenAny(_productsReady.Task,Task.Delay(20000)) != _productsReady.Task) { Busy = false; throw new InvalidOperationException("The store did not answer. Try again later."); }
            await _productsReady.Task;
        }
        public void Buy(Product product) {
            if (_store == null || Busy || !product.availableToPurchase || _account != PlatformClient.Instance.AccountId) return;
            Busy = true; SetStatus("Waiting for store confirmation…"); _store.PurchaseProduct(product);
        }
        public void Restore() {
            if (_store == null || Busy) return;
            SetStatus("Checking previous and pending purchases…");
            _store.RestoreTransactions((ok, _) => { SetStatus(ok ? "Checking pending purchases. Consumed currency is already saved to your account." : "Restore could not be completed. Please try again."); if(ok) _store.FetchPurchases(); });
        }
        private async void OnPending(PendingOrder order) {
            var key = order.Info.TransactionID;
            if (!_verifying.Add(key)) return;
            try {
                Busy = true; SetStatus("Verifying your purchase…");
                var items = order.CartOrdered.Items();
                if (items.Count != 1) throw new InvalidOperationException("This purchase needs support. It has not been granted or confirmed.");
                var body = JsonValue.FromObject(("productId",JsonValue.FromString(items.First().Product.definition.id)));
                if (order.Info.Apple != null) { body.Object["provider"] = JsonValue.FromString("apple"); body.Object["signedTransaction"] = JsonValue.FromString(order.Info.Apple.jwsRepresentation); }
                else if (order.Info.Google != null) { body.Object["provider"] = JsonValue.FromString("google"); body.Object["purchaseToken"] = JsonValue.FromString(order.Info.Google.PurchaseToken); }
                else throw new InvalidOperationException("A valid store receipt is required. No items were granted.");
                await PlatformClient.Instance.Request("POST","/v1/purchases/verify",body);
                _store.ConfirmPurchase(order);
                SetStatus("Purchase verified. Your items are saved to your account.");
            } catch { SetStatus("Purchase verification is pending. Your order has not been discarded. Reopen the shop or tap Restore to retry."); }
            finally { _verifying.Remove(key); Busy = false; Changed?.Invoke(); }
        }
    }
}
