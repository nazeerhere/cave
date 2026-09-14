# Anti-Pyre canonical runtime sprites

`Runtime/AntiPyre/Normal` and `Runtime/AntiPyre/Corrupt` are the sole active
Anti-Pyre body-sprite sets. They are deterministically rebuilt from
`Source/CleanActions` by **Tools > Cave > Enemies > Rebuild Canonical Anti-Pyre
Runtime Assets** and are bound to the prefabs by **Tools > Cave > Enemies > Build
Goth Visuals**.

The sibling `Runtime/Normal` and `Runtime/Corrupt` folders are retained only as
legacy, non-destructive archival output. Do not bind or regenerate from them.
They may be considered for deletion only after a separate, explicitly approved
asset-cleanup pass.
