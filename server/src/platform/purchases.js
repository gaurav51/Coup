import { readFileSync } from "node:fs";
import { SignedDataVerifier, Environment } from "@apple/app-store-server-library";
import { GoogleAuth } from "google-auth-library";
import { hash } from "./auth.js";
import { ApiError, requireValue } from "./errors.js";

/** Store signatures are checked before the wallet transaction. No client-supplied grant amounts. */
export class PurchaseVerifier {
  constructor(env = process.env) {
    this.env = env;
    if (env.APPLE_ROOT_CERT_FILES && env.APPLE_BUNDLE_ID) {
      const roots = env.APPLE_ROOT_CERT_FILES.split(",").map((file) => readFileSync(file.trim()));
      const environment = env.APPLE_STORE_ENV === "sandbox" ? Environment.SANDBOX : Environment.PRODUCTION;
      requireValue(environment !== Environment.PRODUCTION || Number(env.APPLE_APP_ID) > 0, 500, "APPLE_APP_ID is required for production verification.");
      this.apple = new SignedDataVerifier(roots, true, environment, env.APPLE_BUNDLE_ID, Number(env.APPLE_APP_ID) || undefined);
    }
    if (env.GOOGLE_PLAY_PACKAGE) this.google = new GoogleAuth({ scopes: ["https://www.googleapis.com/auth/androidpublisher"] });
  }

  async verify(accountId, product, input) {
    if (input.provider === "apple") {
      requireValue(this.apple && product.apple, 503, "Apple purchases are not configured.");
      requireValue(typeof input.signedTransaction === "string" && input.signedTransaction.length < 24000, 400, "Invalid signed transaction.");
      let tx;
      try { tx = await this.apple.verifyAndDecodeTransaction(input.signedTransaction); }
      catch { throw new ApiError(400, "Apple transaction verification failed."); }
      requireValue(tx.productId === product.apple && tx.appAccountToken?.toLowerCase() === accountId.toLowerCase(), 403, "Purchase product or account mismatch.");
      requireValue(tx.type === "Consumable" && !tx.revocationDate && tx.purchaseDate <= Date.now() + 60000, 409, "Purchase is not a valid consumable.");
      return { transaction: tx.transactionId, accountId, quantity: tx.quantity ?? 1 };
    }
    requireValue(this.google && product.google, 503, "Google Play purchases are not configured.");
    requireValue(typeof input.purchaseToken === "string" && input.purchaseToken.length <= 4096, 400, "Invalid purchase token.");
    const base = this.googleUrl(product.google, input.purchaseToken);
    let data;
    try { ({ data } = await (await this.google.getClient()).request({ url: base, timeout: 10000 })); }
    catch { throw new ApiError(400, "Google purchase verification failed."); }
    requireValue(data.purchaseState === 0 && data.obfuscatedExternalAccountId === hash(accountId), 403, "Purchase is pending, cancelled, or belongs to another account.");
    if (this.env.NODE_ENV === "production" && this.env.GOOGLE_PLAY_ENV !== "test") requireValue(data.purchaseType !== 0, 400, "Test purchases are not accepted by production.");
    return { transaction: hash(input.purchaseToken), accountId, quantity: data.quantity ?? 1,
      finishProof: { token: input.purchaseToken, product: product.google } };
  }

  googleUrl(product, token) {
    return `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${encodeURIComponent(this.env.GOOGLE_PLAY_PACKAGE)}/purchases/products/${encodeURIComponent(product)}/tokens/${encodeURIComponent(token)}`;
  }

  async finish(receipt) {
    if (receipt.provider !== "google" || !receipt.proof) return; // StoreKit finishes on the device after the server grant.
    const client = await this.google.getClient();
    const url = this.googleUrl(receipt.proof.product, receipt.proof.token);
    const { data } = await client.request({ url, timeout: 10000 });
    if (data.consumptionState === 1) return;
    await client.request({ url: `${url}:consume`, method: "POST", timeout: 10000 });
  }
}
