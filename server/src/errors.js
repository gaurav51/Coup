export class GameError extends Error {
  constructor(message) {
    super(message);
    this.name = "GameError";
  }
}

export class RoomError extends Error {
  constructor(message) {
    super(message);
    this.name = "RoomError";
  }
}

export function isRejection(err) {
  return err instanceof GameError || err instanceof RoomError;
}
