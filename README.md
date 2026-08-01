# Warehouse Refill Plus

A quality-of-life mod for **Supermarket Simulator** that improves warehouse ordering, per-product storage limits, market UI controls, and box handling.

## Current source version

`3.2.0`

## Main features

- Adds a warehouse refill button to the market application.
- Calculates how many boxes are already stored on assigned warehouse racks.
- Adds only the missing number of boxes to the shopping cart.
- Includes boxes that are already present in the cart when calculating the refill amount.
- Allows a separate `Max` value for each product.
- Uses the combined capacity of all assigned rack slots for the product.
- Saves custom limits in `SmartCartLimits.txt` inside the game's persistent data directory.
- Adds controls for resetting all custom limits and clearing the shopping cart.
- Allows clearing the active cart with `F2`.
- Allows placing a held product box on a matching warehouse rack with `F10`.
- Automatically tries to place delivered product boxes on matching warehouse rack slots.
- Removes the normal late-order restriction from the market shopping cart.

## How the refill calculation works

The refill amount is calculated as:

```text
selected product limit - boxes already stored - boxes already in cart
```

The result is never allowed to go below zero.

### Example

A product has a `Max` value of `4`.

- `2` boxes are already stored on assigned racks.
- `1` box is already in the shopping cart.
- Pressing the refill button adds only `1` additional box.

## Combined rack capacity

The maximum value is based on the total capacity of every rack slot assigned to the product.

Example: three assigned rack slots can each hold one box. The product limit can therefore be set to `3`, rather than being limited to the capacity of a single slot.

## Version 3.2.0

Version 3.2.0 adds automatic delivery handling, the `F10` held-box shortcut, and performance improvements for the market UI.

Delivered boxes are placed through the game's native `RackSlot.AddBox` logic. The mod stops the box physics first, lets the rack slot assign the correct parent, position and rotation, and only then marks the box as racked and static. This behavior should not be replaced with manual transform positioning without full regression testing.

The market capacity calculation uses a cached rack snapshot instead of rescanning every rack slot for every product entry. The snapshot is refreshed when needed and expires after a short interval.

## Important maintenance notes

- Do not manually set the delivered box parent, local position, or local rotation before `RackSlot.AddBox`.
- Do not remove the existing-stock or existing-cart subtraction from the refill calculation.
- Do not change the combined-rack capacity behavior back to single-slot capacity.
- Keep the performance cache unless a replacement is tested with many products and racks.
- Test automatic delivery separately from the `F10` held-box path because they use different placement methods.

See [`STATUS.md`](STATUS.md) for maintenance rules and [`TESTS.md`](TESTS.md) for the regression checklist.
