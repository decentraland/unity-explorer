# Upgrade

These guides will help you upgrade your project to use the latest version of *KTX for Unity*. If you still encounter problems, help us improving this guide and *KTX for Unity* in general by reaching out or by raising an issue.

## Unity Fork

With the release of version 3.2.0-pre.1 the package name and identifier were changed to *KTX for Unity* (`com.unity.cloud.ktx`) for the following reasons:

- Better integration into Unity internal development processes (including quality assurance and support)
- Distribution via the Unity Package Manager (no scoped libraries required anymore)

For now, both the Unity variant and the original version will receive updates.

### Transition to *KTX for Unity*

The C# namespaces are identical between the variants, so all you need to do is:

- Removed original *KTX/Basis Texture Unity Package* (with package identifier `com.atteneder.ktx`).
- Add *KTX for Unity* (`com.unity.cloud.ktx`).
- Update assembly definition references (if your project had any).
- Update any dependencies in your packages manifest (if your package had any)

### Keep using the original KTX/Basis Texture Unity Package

The original *KTX/Basis Texture Unity Package* (`com.atteneder.ktx`) will still receive identical updates for now. You may choose to continue using it.

If you've installed the packages via the installer script (i.e. via [OpenUPM][OpenUPM] scoped registry - the recommended way), you don't need to change anything. You'll receive updates as usual.

See [Original *KTX/Basis Texture Unity Package*](./Original.md) for instructions to install the original version from scratch.

## Trademarks

*Unity* is a registered trademark of [Unity Technologies][unity].

Khronos&reg; and the Khronos Group logo are registered trademarks of the [The Khronos Group Inc][khronos].

KTX&trade; and the KTX logo are trademarks of the [The Khronos Group Inc][khronos].

[khronos]: https://www.khronos.org
[OpenUPM]: https://openupm.com/
[unity]: https://unity.com
