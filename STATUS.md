# Project status

## Status

`RELEASED_UPDATE`

## Current source version

`3.2.0`

## Confirmed implementation

- Warehouse refill button in the market application.
- Per-product `Max` values.
- Limits based on the combined capacity of all assigned rack slots.
- Refill amount reduced by boxes already stored.
- Refill amount reduced by matching boxes already in the cart.
- Custom limits saved in `SmartCartLimits.txt`.
- Reset-all-limits control.
- Clear-cart control and `F2` shortcut.
- `F10` shortcut for placing the held product box on a matching rack.
- Automatic placement attempt for delivered product boxes.
- Cached market-capacity calculation.
- Late-order restriction bypass for the market cart.

## Critical version 3.2.0 delivery behavior

Automatic delivery and held-box placement deliberately use different paths.

### Delivered boxes

Delivered boxes are not held by the player. The current implementation:

1. Finds a matching, non-full `RackSlot`.
2. Stops linear and angular Rigidbody velocity.
3. Sets the Rigidbody to kinematic.
4. Calls `RackSlot.AddBox(box.BoxID, box, true)`.
5. Sets `box.Racked = true`.
6. Calls `box.SetStatic(true)` when available.

`RackSlot.AddBox` must remain responsible for the final parent, local position, local rotation and slot arrangement.

Do not restore the older behavior that manually changed the box transform before native placement.

### Held boxes

A box physically held by the local player is placed through the game's normal interaction path:

- set `BoxInteraction.m_CurrentRackSlot`,
- call `BoxInteraction.PlaceBoxToRack()`,
- clear `m_CurrentRackSlot`.

This preserves the game's normal placement behavior, animation and sound.

## Refill calculation rules

For every assigned product:

```text
amount to order = max(0, selected limit - stored box count - matching cart count)
```

The default selected limit is the combined capacity of all assigned slots for that product.

Do not:

- count empty, unassigned rack slots as product capacity,
- ignore boxes already stored,
- ignore boxes already present in the cart,
- reduce the maximum back to one-slot capacity.

## Performance safeguards

`MarketAppUIEnhancer.GetMaxBoxCapacity` is replaced with a cached implementation.

The cache:

- stores combined capacity by product,
- stores single-slot capacity by product,
- rebuilds after its lifetime expires,
- rebuilds when the rack count changes,
- clears when relevant manager instances change,
- avoids repeated rack scans for every market product entry.

Do not remove the cache without testing a store containing many products and warehouse racks.

## Persistent data

Custom product limits are stored in:

```text
Application.persistentDataPath/SmartCartLimits.txt
```

Each line uses:

```text
productId:limit
```

Entering `0` or an empty value removes the custom limit and restores calculated capacity.

## Release rule

A successful build is not enough. Version 3.2.0 should be considered ready only after the tests in `TESTS.md`, especially automatic delivery, `F10`, refill arithmetic and save/reload of limits.
