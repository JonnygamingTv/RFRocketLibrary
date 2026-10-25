# RFRocketLibrary.Tests

Unit tests for RFRocketLibrary runnable on a plain .NET Framework 4.8.1 host
(no game server required).

## Scope and verification boundary

Covered without the game:

- `SerialQueue` ordering, exception isolation, async chaining.
- `JsonDataStore<T>` save/load round trips, atomic-write behavior, temp-file hygiene.
- `StringTypeConverter` value-type parsing (bool/int/ushort/string aliases and failures).

Not covered here (requires a running Unturned server; documented on purpose):

- Asset-backed parsing (`ItemAsset` / `VehicleAsset` / `AnimalAsset` lookups),
  game event invokers, Harmony patch events, and `ThreadTool`/`FastTaskDispatcher`
  main-thread dispatch — all of them need live Unity engine callbacks.

## The bundled `netstandard.dll`

Rocket's logging chain bottoms out in `SDG.Unturned.UnturnedLog`, whose static
constructor subscribes to `UnityEngine.Application.logMessageReceived`. Type
resolution for that chain requires the **netstandard 2.1.0.0 facade** that
Unturned servers ship with them (`Unturned_Data/Managed/netstandard.dll`); a
plain .NET Framework host does not provide one. The file next to this csproj is
a copy of that server-distributed facade (90,624 bytes, sha of the Unturned
server distribution), so the storage error paths can execute outside the game.
It is never loaded by the library itself on a real server — servers already
carry their own copy.
