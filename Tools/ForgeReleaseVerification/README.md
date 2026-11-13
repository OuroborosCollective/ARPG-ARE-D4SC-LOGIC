# Release-only verification driver

These sources deliberately stay outside `Assets/` in the game checkout. They
must not be compiled into Ashen Crypt or exported with the commercial package.
Unity imports them only after this explicit preparation step in a separate URP
verification project:

```sh
python3 scripts/forge/prepare_release_verification.py /absolute/path/to/verification-project
```

Import the Memory Forge package into that project first and install the matching
Unity 6 URP package. The script copies this tree to
`Assets/ForgeReleaseVerification`, where Unity imports both assembly definitions.
It refuses the game checkout, a missing package, and overwriting existing files
with different contents. Its JSON receipt fingerprints each copied source.

After Unity has compiled, the actual build entry points are
`AshenCrypt.ForgeVerification.Editor.ReleaseBuilder.BuildLinuxEvidence` and
`AshenCrypt.ForgeVerification.Editor.ReleaseBuilder.BuildAndroidExample`.
Set `FORGE_PLAYER_OUTPUT` to the Linux executable path or `FORGE_ANDROID_OUTPUT`
to the APK path. Run `ConfigureAndAuthor` first to author the included demo scene
and URP settings. Set `FORGE_EVIDENCE_DIR` when executing the verification player.
`PlayerEvidence` is a verification driver, not shipped example behavior; a build
or automated fixture is not a physical-device test.
