# Regression tests

## Startup

- [ ] The game starts without exceptions from Warehouse Refill Plus.
- [ ] BepInEx loads `Warehouse Refill Plus` version `3.2.0`.
- [ ] Harmony patches are applied once.
- [ ] The market application opens without a large freeze.
- [ ] The custom UI manager is not duplicated after scene changes.

## Market UI

- [ ] The warehouse refill button appears only in the market view.
- [ ] The reset-limits button appears in the market view.
- [ ] The clear-cart button appears near the purchase controls.
- [ ] `F2` clears the current cart.
- [ ] Opening and closing the computer repeatedly does not duplicate buttons.
- [ ] Product rows receive only one `Max` control.

## Combined rack capacity

- [ ] One assigned slot uses the correct box capacity.
- [ ] Two or more assigned slots are added together.
- [ ] Different products do not share capacity.
- [ ] A product with no assigned rack uses the safe single-slot fallback.
- [ ] Existing box count above the calculated slot capacity does not produce a lower maximum.
- [ ] Adding or removing racks eventually refreshes the cached result.

## Editing limits

- [ ] Clicking `Max` opens numeric editing.
- [ ] Values are clamped to at least `1`.
- [ ] Values are clamped to the calculated combined capacity.
- [ ] Entering `0` removes the custom limit.
- [ ] Empty input restores the calculated default.
- [ ] Reset Limits removes all custom values.
- [ ] Limits remain correct after reopening the market application.

## Saving and loading limits

- [ ] `SmartCartLimits.txt` is created after saving a custom limit.
- [ ] The file contains `productId:limit`.
- [ ] Saved limits load after restarting the game.
- [ ] Removing a limit updates the file.
- [ ] Resetting all limits clears the stored limits.
- [ ] A malformed line does not prevent the mod from loading.

## Refill arithmetic

Test one product with a known selected limit.

- [ ] No stored boxes and an empty cart add the full selected limit.
- [ ] Stored boxes reduce the added amount.
- [ ] Matching boxes already in the cart reduce the added amount.
- [ ] Stored boxes and cart boxes are both subtracted.
- [ ] The result never becomes negative.
- [ ] A full warehouse adds nothing for that product.
- [ ] The cart total price updates after products are added.
- [ ] Pressing refill repeatedly does not exceed the selected limit.

## Automatic delivery

- [ ] Delivered product boxes are detected after delivery completes.
- [ ] Furniture boxes are ignored.
- [ ] Invalid or productless boxes are ignored.
- [ ] A delivered box goes to a matching non-full rack slot.
- [ ] The box receives the correct parent, local position and rotation from `RackSlot.AddBox`.
- [ ] The delivered box does not remain on the street.
- [ ] The delivered box does not fall, spin or launch after placement.
- [ ] `box.Racked` is true after successful placement.
- [ ] The box is static after placement.
- [ ] Multiple delivered boxes fill available matching slots without overlap.
- [ ] A box remains unplaced when no matching free slot exists.
- [ ] No box is duplicated or lost after saving and reloading.

## Held-box shortcut — F10

- [ ] Holding a product box and pressing `F10` places it on a matching free rack.
- [ ] The normal placement animation and sound still work.
- [ ] `m_CurrentRackSlot` is cleared after placement.
- [ ] A furniture box is ignored.
- [ ] A box with no matching free slot remains held.
- [ ] Repeated `F10` presses do not duplicate the box.
- [ ] This path works independently from automatic delivery.

## Ordering time restriction

- [ ] The market cart is not blocked by `TooLateToOrderGoods`.
- [ ] Ordering outside the normal time window still completes correctly.
- [ ] Bypassing the restriction does not break cart totals or purchase completion.

## Performance

- [ ] Opening a market with many products does not rescan all racks for every product row.
- [ ] UI jobs are processed gradually without duplicating controls.
- [ ] No repeated exception logging occurs in the capacity hot path.
- [ ] Changing scenes clears stale UI queues and cached references.
- [ ] Testing with many racks does not cause sustained frame-time spikes.

## Release result

Version 3.2.0 is ready only after:

- [ ] Refill arithmetic passes.
- [ ] Custom limits survive restart.
- [ ] Automatic delivery passes.
- [ ] `F10` held-box placement passes.
- [ ] Save/reload produces no duplicated or misplaced boxes.
- [ ] Market UI performance remains acceptable with many products and racks.
