export class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}
export function requireValue(condition, status, message) {
  if (!condition) throw new ApiError(status, message);
}
export function displayName(value = "Player") {
  requireValue(typeof value === "string", 400, "Invalid display name.");
  const name = value.trim().replace(/\s+/g, " ");
  requireValue(name.length >= 1 && name.length <= 16 && !/[\x00-\x1f<>]/.test(name), 400, "Use a name of 1–16 characters without markup.");
  return name;
}
