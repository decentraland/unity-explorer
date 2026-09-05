# Setup

## SSH

In order to build the unity-explorer project you will need to ensure you have SSH setup with correct access rights to the private repository.

To do this:

1. Generating a new SSH key: [https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent#generating-a-new-ssh-key](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent#generating-a-new-ssh-key)

2. Adding a new SSH key to your account: [https://docs.github.com/en/authentication/connecting-to-github-with-ssh/adding-a-new-ssh-key-to-your-github-account](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/adding-a-new-ssh-key-to-your-github-account)

3. To be able to correctly import the UPM packages that need SSH make sure to follow this short guide: https://docs.unity3d.com/Manual/upm-config-ssh-git-win.html

Notes:

- You may need to update the known_hosts file in your ssh keys directory: [https://github.blog/2023-03-23-we-updated-our-rsa-ssh-host-key/](https://github.blog/2023-03-23-we-updated-our-rsa-ssh-host-key/)
- If the repo cloning is done with a terminal, you may need to open it as an administrator to avoid a `fetch-pack: unexpected disconnect while reading sideband packet` error
- If during the Unity project loading Unity throws an error window mid-loading with a `fetch-pack: unexpected disconnect while reading sideband packet` error, try using the "retry" button, it has proved to work...

## GPG

GPG keys are used to sign commits and verify that it was sent from your computer with your user. To create your GPG key, follow the instructions on this page: [https://docs.github.com/en/authentication/managing-commit-signature-verification/generating-a-new-gpg-key](https://docs.github.com/en/authentication/managing-commit-signature-verification/generating-a-new-gpg-key)

Do not forget to do everything in the GitBash console.

### Troubleshooting

1. If you get this error:

> gpg: skipped "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX": No secret key
> gpg: signing failed: No secret key
> error: gpg failed to sign the data
> fatal: failed to write commit object

Add this to the .gitconfig file (at `C:/Users/<user>/`):

```
[gpg]
	program = "C:\\Program Files\\Git\\usr\\bin\\gpg.exe"
```

2. If you get this error when sending a commit:

> GPG does not find the agent after restarting the PC:
> gpg: error running '/usr/lib/gnupg/keyboxd': probably not installed
> gpg: failed to start keyboxd '/usr/lib/gnupg/keyboxd': Configuration error
> gpg: can't connect to the keyboxd: Configuration error
> gpg: error opening key DB: No Keybox daemon running
> gpg: skipped "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX": Input/output error
> gpg: signing failed: Input/output error
> error: gpg failed to sign the data
> fatal: failed to write commit object

Open the GitBash console and execute:

```bash
gpg --list-secret-keys --keyid-format SHORT
```

### Verification

To verify that GPG signing is working, send a commit attached to a GitHub task (putting its task #number in the description) and check how the Verified tag appears next to the hash of the commit.

![imagen](https://github.com/user-attachments/assets/d23220ae-cb45-4ad7-b7b2-5a83c0861961)

## LODs

By default LODs are currently disabled in Explorer Alpha because they are too big to be packaged with the build. They can however be downloaded and integrated manually for testing.

1. LODs can be downloaded here (latest): https://drive.google.com/file/d/1YoYpi-FhXL3IxNTOptazWquvv4VRCLbL/view?usp=drive_link

2. To get LODs working you just need to extract the above into the following folder: `StreamingAssets/AssetBundles/lods`

## Build Automation & CI

For documentation on GitHub workflows, the Python build handler, and Unity Cloud setup, see [Build & CI](build-and-ci.md).
