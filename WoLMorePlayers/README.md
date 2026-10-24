# More Players (Wizard of Legend) - EXPERIMENTAL

A first step towards more than two players. **It doesn't add a third player yet**: it raises the
game's player limit (`PlayerLimit`, default 3; the game's own is 2) before the game sets itself
up, and writes what the game and its input library (Rewired) report to
`BepInEx\LogOutput.log`, to find what stands in the way of a third player joining.

## Testing

1. Install it, plug in all three controllers, start the game.
2. In the house, try to join with each controller the way the second player normally joins.
3. Send the lines starting `More Players` from the log.

Turn it off (`Enabled = false` in `BepInEx\config\mdbailey94.wol.moreplayers.cfg`, or remove the
folder) if anything misbehaves.
