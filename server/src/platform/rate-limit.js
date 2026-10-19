/** Bounded token buckets: do not let arbitrary IPs/event names grow memory without limit. */
export class RateLimit {
  constructor({ capacity = 60, refillPerSecond = 1, maxKeys = 10000 } = {}) {
    this.capacity = capacity; this.refill = refillPerSecond; this.maxKeys = maxKeys; this.buckets = new Map();
  }
  take(key, now = Date.now()) {
    let b = this.buckets.get(key);
    if (!b) {
      if (this.buckets.size >= this.maxKeys) {
        for (const [k, value] of this.buckets) if (now - value.at > 300000) this.buckets.delete(k);
        if (this.buckets.size >= this.maxKeys) return false;
      }
      b = { tokens: this.capacity, at: now };
    }
    b.tokens = Math.min(this.capacity, b.tokens + (now - b.at) * this.refill / 1000); b.at = now;
    this.buckets.set(key, b);
    if (b.tokens < 1) return false;
    b.tokens--; return true;
  }
}
